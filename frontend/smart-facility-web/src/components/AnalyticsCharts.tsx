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

const palette = ['#155e75', '#0f766e', '#2563eb', '#64748b', '#d97706', '#7c3aed']

export function HorizontalBarChart({ data, maxItems = 12 }: { data: ChartDatum[]; maxItems?: number }) {
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
        grid: { color: '#e8edf3' },
        ticks: { callback: (value) => formatCount(Number(value)) },
      },
      y: {
        grid: { display: false },
        ticks: { autoSkip: false },
      },
    },
  }

  return (
    <>
      <div
        className="chart-canvas"
        style={{ height: `${Math.max(260, visible.length * 34)}px` }}
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
                borderRadius: 5,
                borderSkipped: false,
              },
            ],
          }}
        />
      </div>
      {data.length > visible.length ? (
        <p className="chart-limit-note">En yüksek {visible.length} kategori gösteriliyor.</p>
      ) : null}
      <ul className="visually-hidden">
        {visible.map((item) => (
          <li key={item.label}>{item.label}: {formatCount(item.count)}</li>
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
      x: { grid: { display: false } },
      y: {
        beginAtZero: true,
        grid: { color: '#e8edf3' },
        ticks: { callback: (value) => formatCount(Number(value)) },
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
              borderColor: '#155e75',
              backgroundColor: 'rgba(21, 94, 117, 0.12)',
              pointBackgroundColor: '#155e75',
              pointRadius: 3,
              pointHoverRadius: 5,
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
