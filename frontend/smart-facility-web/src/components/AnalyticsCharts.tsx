import {
  BarElement,
  CategoryScale,
  Chart as ChartJs,
  Filler,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Tooltip,
  type ChartOptions,
} from 'chart.js'
import { Bar, Line } from 'react-chartjs-2'
import type { KpiReliability, TrendPoint } from '../api/analyticsTypes'
import { formatCount, formatMonth } from '../utils/format'
import { EmptyState, ReliabilityBadge } from './DashboardUi'

ChartJs.register(
  CategoryScale,
  LinearScale,
  BarElement,
  LineElement,
  PointElement,
  Tooltip,
  Legend,
  Filler,
)

export interface ChartDatum {
  label: string
  count: number
}

interface ChartPanelProps {
  title: string
  subtitle?: string
  reliability?: KpiReliability
  children: React.ReactNode
}

export function ChartPanel({ title, subtitle, reliability, children }: ChartPanelProps) {
  return (
    <section className="chart-panel">
      <header className="chart-panel__header">
        <div>
          <h2>{title}</h2>
          {subtitle ? <p>{subtitle}</p> : null}
        </div>
        {reliability ? <ReliabilityBadge reliability={reliability} /> : null}
      </header>
      {children}
    </section>
  )
}

const palette = ['#df0025', '#414141', '#595858', '#a51129', '#828282', '#242424']
const chartFontFamily = '"Open Sans", "Segoe UI", Arial, sans-serif'

export function HorizontalBarChart({
  data,
  maxItems = 12,
  compact = false,
}: {
  data: ChartDatum[]
  maxItems?: number
  compact?: boolean
}) {
  if (data.length === 0) {
    return <EmptyState />
  }

  const visible = data.slice(0, maxItems)
  const options: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y',
    animation: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (context) => formatCount(Number(context.raw)),
        },
      },
    },
    scales: {
      x: {
        beginAtZero: true,
        border: { display: false },
        grid: { color: '#e1dddd' },
        ticks: { color: '#595858', font: { family: chartFontFamily, size: 10 }, callback: (value) => formatCount(Number(value)) },
      },
      y: {
        border: { display: false },
        grid: { display: false },
        ticks: { autoSkip: false, color: '#414141', font: { family: chartFontFamily, size: 10, weight: 600 } },
      },
    },
  }

  return (
    <>
      <div
        className="chart-canvas"
        style={{ height: `${Math.max(compact ? 180 : 230, visible.length * (compact ? 27 : 30))}px` }}
        role="img"
        aria-label={`${visible.length} kategorili yatay çubuk grafik`}
      >
        <Bar
          options={options}
          data={{
            labels: visible.map((item) => item.label),
            datasets: [
              {
                data: visible.map((item) => item.count),
                backgroundColor: visible.map((_, index) => palette[index % palette.length]),
                borderRadius: 3,
                borderSkipped: false,
                maxBarThickness: 20,
              },
            ],
          }}
        />
      </div>
      {data.length > visible.length ? (
        <p className="chart-limit-note">En yüksek {visible.length} kategori gösteriliyor.</p>
      ) : null}
      <ul className="visually-hidden">
        {visible.map((item, index) => (
          <li key={`${item.label}-${index}`}>{item.label}: {formatCount(item.count)}</li>
        ))}
      </ul>
    </>
  )
}

export function TrendLineChart({ points }: { points: TrendPoint[] }) {
  if (points.length === 0) {
    return <EmptyState />
  }

  const options: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (context) => formatCount(Number(context.raw)),
        },
      },
    },
    scales: {
      x: {
        border: { display: false },
        grid: { display: false },
        ticks: { color: '#595858', font: { family: chartFontFamily, size: 10 } },
      },
      y: {
        beginAtZero: true,
        border: { display: false },
        grid: { color: '#e1dddd' },
        ticks: { color: '#595858', font: { family: chartFontFamily, size: 10 }, callback: (value) => formatCount(Number(value)) },
      },
    },
  }

  return (
    <div className="chart-canvas chart-canvas--line" role="img" aria-label="Aylık trend çizgi grafiği">
      <Line
        options={options}
        data={{
          labels: points.map((point) => formatMonth(point.period)),
          datasets: [
            {
              data: points.map((point) => point.count),
              borderColor: '#df0025',
              backgroundColor: 'rgba(223, 0, 37, 0.07)',
              pointBackgroundColor: '#df0025',
              pointRadius: 2,
              pointHoverRadius: 4,
              borderWidth: 2,
              tension: 0.25,
              fill: true,
            },
          ],
        }}
      />
    </div>
  )
}
