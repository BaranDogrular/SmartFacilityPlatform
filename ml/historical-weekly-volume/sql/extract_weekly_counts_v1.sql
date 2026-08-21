SET NOCOUNT ON;

-- historical-weekly-volume/v1
-- Read-only, deterministic calendar spine. Weeks are [Monday, next Monday).
WITH WeekSeries AS
(
    SELECT CAST('2022-05-23' AS date) AS WeekStart

    UNION ALL

    SELECT DATEADD(day, 7, WeekStart)
    FROM WeekSeries
    WHERE WeekStart < CAST('2026-07-27' AS date)
)
SELECT
    CONVERT(char(10), weeks.WeekStart, 23) AS WeekStart,
    CONVERT(char(10), DATEADD(day, 7, weeks.WeekStart), 23) AS WeekEndExclusive,
    COUNT_BIG(historical.Id) AS HistoricalCount
FROM WeekSeries AS weeks
LEFT JOIN analytics.HistoricalWorkOrders AS historical
    ON historical.ReportedDateTime >= weeks.WeekStart
    AND historical.ReportedDateTime < DATEADD(day, 7, weeks.WeekStart)
GROUP BY weeks.WeekStart
ORDER BY weeks.WeekStart
OPTION (MAXRECURSION 0);
