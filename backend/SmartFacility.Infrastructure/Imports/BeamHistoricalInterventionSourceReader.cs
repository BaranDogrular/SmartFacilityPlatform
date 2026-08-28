using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ExcelDataReader;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;

namespace SmartFacility.Infrastructure.Imports;

public sealed class BeamHistoricalInterventionSourceReader : IHistoricalInterventionSourceReader
{
    private static readonly string[] ExpectedHeaders =
    [
        "İş Emri Yılı", "İş Emri No", "İş Emri Durumu", "Varlık Kodu", "Varlık Tanımı",
        "Bildiriliş Tarihi", "Bildiriliş Saati", "Başlangıç Tarihi", "Başlangıç Saati",
        "Bitiş Tarihi", "Bitiş Saati", "Devreye Alma Tarihi", "Devreye Alma Saati",
        "Üst Sahip Varlık Kodu", "Üst Sahip Varlık Adı", "Varlık Öncelik Kodu",
        "Bakım Öncelik Tanımı", "Açıklama", "Yapılan İşin Açıklaması", "Arıza Nedeni Kodu",
        "Arıza Nedeni Tanımı", "İletişim", "Talep Eden", "Sorumlu Personel Kodu",
        "Sorumlu Personel Tanımı", "Bildirildiği Vardiya Kodu", "Değerlendirme Puanı",
        "Bakım Süresi", "Duruş Süresi", "İşçilik Süresi", "Malzeme Maliyeti",
        "İşçilik Maliyeti", "Toplam Maliyet", "Toplam Maliyet (Döviz)"
    ];

    static BeamHistoricalInterventionSourceReader() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public Task<HistoricalInterventionSourceReadResult> ReadAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Task.FromResult(Read(filePath, cancellationToken));
    }

    private static HistoricalInterventionSourceReadResult Read(
        string filePath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(filePath);
        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Historical Intervention source file was not found.", fullPath);
        }

        var hash = CalculateSha256(fullPath);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(
            stream,
            new ExcelReaderConfiguration { FallbackEncoding = Encoding.GetEncoding(1254) });

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!reader.Read() || !HeaderMatches(reader))
            {
                continue;
            }

            var sheetName = reader.Name;
            var rows = new List<HistoricalInterventionSourceRow>();
            var errors = new List<string>();
            var physicalRows = 1;
            DateTime? minReportedAt = null;
            DateTime? maxReportedAt = null;

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsEmpty(reader))
                {
                    continue;
                }

                physicalRows++;
                var rowNumber = reader.Depth + 1;
                if (!TryParseRequiredIdentity(reader, out var sourceYear, out var number,
                        out var reportedAt, out var assetCode, out var error))
                {
                    errors.Add($"{file.Name}/{sheetName}!{rowNumber}: {error}");
                    continue;
                }

                var completion = CombineDateAndTime(reader.GetValue(9), reader.GetValue(10));
                rows.Add(new HistoricalInterventionSourceRow(
                    fullPath,
                    file.Name,
                    hash,
                    sheetName,
                    rowNumber,
                    sourceYear,
                    number,
                    reportedAt,
                    assetCode,
                    Text(reader.GetValue(2)),
                    Text(reader.GetValue(4)),
                    completion,
                    Text(reader.GetValue(17)),
                    Text(reader.GetValue(18)),
                    Text(reader.GetValue(19)),
                    Text(reader.GetValue(20)),
                    Text(reader.GetValue(27)),
                    Text(reader.GetValue(28)),
                    Text(reader.GetValue(29)),
                    Text(reader.GetValue(30)),
                    Text(reader.GetValue(31)),
                    Text(reader.GetValue(32)),
                    Text(reader.GetValue(33))));
                minReportedAt = !minReportedAt.HasValue || reportedAt < minReportedAt
                    ? reportedAt
                    : minReportedAt;
                maxReportedAt = !maxReportedAt.HasValue || reportedAt > maxReportedAt
                    ? reportedAt
                    : maxReportedAt;
            }

            return new HistoricalInterventionSourceReadResult(
                new HistoricalInterventionSourceFileSummary(
                    fullPath,
                    file.Name,
                    hash,
                    file.Length,
                    sheetName,
                    physicalRows,
                    rows.Count,
                    minReportedAt,
                    maxReportedAt),
                rows,
                errors);
        }
        while (reader.NextResult());

        return new HistoricalInterventionSourceReadResult(
            new HistoricalInterventionSourceFileSummary(
                fullPath,
                file.Name,
                hash,
                file.Length,
                string.Empty,
                0,
                0,
                null,
                null),
            [],
            [$"{file.Name}: no worksheet has the expected 34-column BEAM Varlık Tarihçesi header."]);
    }

    private static bool HeaderMatches(IExcelDataReader reader)
    {
        if (reader.FieldCount < ExpectedHeaders.Length)
        {
            return false;
        }

        for (var index = 0; index < ExpectedHeaders.Length; index++)
        {
            if (!string.Equals(
                    Text(reader.GetValue(index)),
                    ExpectedHeaders[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseRequiredIdentity(
        IExcelDataReader reader,
        out int sourceYear,
        out string workOrderNumber,
        out DateTime reportedAt,
        out string assetCode,
        out string error)
    {
        sourceYear = 0;
        workOrderNumber = Text(reader.GetValue(1)) ?? string.Empty;
        assetCode = Text(reader.GetValue(3)) ?? string.Empty;
        reportedAt = default;
        error = string.Empty;

        if (!TryParseYear(reader.GetValue(0), out sourceYear))
        {
            error = "İş Emri Yılı is not a supported integer year.";
            return false;
        }

        if (workOrderNumber.Length == 0 || assetCode.Length == 0)
        {
            error = "strict identity requires İş Emri No and Varlık Kodu.";
            return false;
        }

        var combined = CombineDateAndTime(reader.GetValue(5), reader.GetValue(6));
        if (!combined.HasValue)
        {
            error = "strict identity requires a parseable Bildiriliş Tarihi/Saati.";
            return false;
        }

        reportedAt = combined.Value;
        return true;
    }

    private static bool TryParseYear(object? value, out int year)
    {
        if (value is double number)
        {
            year = checked((int)number);
            return number == year && year is >= 2000 and <= 2100;
        }

        return int.TryParse(Text(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out year)
            && year is >= 2000 and <= 2100;
    }

    private static DateTime? CombineDateAndTime(object? dateValue, object? timeValue)
    {
        if (!TryParseDate(dateValue, out var date))
        {
            return null;
        }

        return date.Date.Add(TryParseTime(timeValue, out var time) ? time : TimeSpan.Zero);
    }

    private static bool TryParseDate(object? value, out DateTime date)
    {
        if (value is DateTime dateTime)
        {
            date = dateTime;
            return true;
        }

        if (value is double serial && serial is > 0 and < 2958466)
        {
            date = DateTime.FromOADate(serial);
            return true;
        }

        var text = Text(value);
        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"),
                   DateTimeStyles.AllowWhiteSpaces, out date)
               || DateTime.TryParse(text, CultureInfo.InvariantCulture,
                   DateTimeStyles.AllowWhiteSpaces, out date);
    }

    private static bool TryParseTime(object? value, out TimeSpan time)
    {
        if (value is TimeSpan timeSpan)
        {
            time = timeSpan;
            return true;
        }

        if (value is DateTime dateTime)
        {
            time = dateTime.TimeOfDay;
            return true;
        }

        if (value is double serial && serial is >= 0 and < 1)
        {
            time = TimeSpan.FromDays(serial);
            return true;
        }

        var text = Text(value);
        return TimeSpan.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"), out time)
               || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time)
               || (DateTime.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"),
                       DateTimeStyles.AllowWhiteSpaces, out var parsed)
                   && (time = parsed.TimeOfDay) >= TimeSpan.Zero);
    }

    private static bool IsEmpty(IExcelDataReader reader)
    {
        for (var index = 0; index < Math.Min(reader.FieldCount, ExpectedHeaders.Length); index++)
        {
            if (reader.GetValue(index) is not null
                && !string.IsNullOrWhiteSpace(reader.GetValue(index)?.ToString()))
            {
                return false;
            }
        }

        return true;
    }

    private static string? Text(object? value)
    {
        var text = value switch
        {
            null => null,
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan time => time.ToString("c", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
        return HistoricalInterventionTextNormalizer.NormalizeOriginal(text);
    }

    private static string CalculateSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
