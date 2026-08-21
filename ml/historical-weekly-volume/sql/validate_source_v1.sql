SET NOCOUNT ON;

-- historical-weekly-volume/v1 source contract validation. Read-only.
SELECT
    COUNT_BIG(*) AS TotalSourceRows,
    SUM(CASE
        WHEN ReportedDateTime >= CAST('2022-05-23' AS datetime2)
         AND ReportedDateTime < CAST('2026-08-03' AS datetime2)
        THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS IncludedSourceRows,
    SUM(CASE
        WHEN ReportedDateTime < CAST('2022-05-23' AS datetime2)
        THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS RowsBeforeFirstCompleteWeek,
    SUM(CASE
        WHEN ReportedDateTime >= CAST('2026-08-03' AS datetime2)
        THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS RowsAtOrAfterCutoff,
    SUM(CASE
        WHEN ReportedDateTime IS NULL
        THEN CAST(1 AS bigint) ELSE CAST(0 AS bigint) END) AS MissingEventTimeRows,
    MIN(ReportedDateTime) AS MinEventDateTime,
    MAX(ReportedDateTime) AS MaxEventDateTime
FROM analytics.HistoricalWorkOrders;
