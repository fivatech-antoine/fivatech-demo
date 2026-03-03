import { useCallback, useEffect, useState } from 'react'
import { getDepartures } from '../../services/api'
import type { Departure, Stop } from '../../types'
import './DepartureBoard.css'

interface Props {
  stop: Stop
  selectedDeparture?: Departure | null
  onDepartureSelect?: (dep: Departure) => void
}

function fmtTime(iso: string) {
  return new Date(iso).toLocaleTimeString('fr-CH', { hour: '2-digit', minute: '2-digit' })
}

function fmtDelay(seconds: number | undefined) {
  if (seconds == null) return null
  if (seconds === 0) return { label: 'à l\'heure', cls: 'departure-board__delay--ok' }
  const m = Math.round(seconds / 60)
  return {
    label: m > 0 ? `+${m} min` : `${m} min`,
    cls: m > 0 ? 'departure-board__delay--late' : 'departure-board__delay--early',
  }
}

interface Filters {
  ligne: string
  operator: string
  direction: string
}

export function DepartureBoard({ stop, selectedDeparture, onDepartureSelect }: Props) {
  const [departures, setDepartures] = useState<Departure[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null)
  const [filters, setFilters] = useState<Filters>({ ligne: '', operator: '', direction: '' })

  const setFilter = (key: keyof Filters, value: string) =>
    setFilters(f => ({ ...f, [key]: value }))

  const unique = (arr: string[]) => [...new Set(arr)].sort()
  const lignes     = unique(departures.map(d => d.routeShortName))
  const operators  = unique(departures.map(d => d.operator).filter((o): o is string => o !== undefined))
  const directions = unique(departures.map(d => d.headsign))

  const filtered = departures.filter(d =>
    (!filters.ligne     || d.routeShortName === filters.ligne) &&
    (!filters.operator  || d.operator       === filters.operator) &&
    (!filters.direction || d.headsign       === filters.direction)
  )

  const refresh = useCallback(() => {
    getDepartures(stop.stopId)
      .then(data => {
        setDepartures(data)
        setLastUpdate(new Date())
        setLoading(false)
        setError(null)
      })
      .catch(() => {
        setError('Impossible de charger les départs')
        setLoading(false)
      })
  }, [stop.stopId])

  useEffect(() => {
    setLoading(true)
    setDepartures([])
    refresh()
    const id = setInterval(refresh, 30_000)
    return () => clearInterval(id)
  }, [refresh])

  return (
    <div className="departure-board">
      <div className="departure-board__header">
        <span className="departure-board__stop-name">{stop.name}</span>
        {lastUpdate && (
          <span className="departure-board__updated">
            {lastUpdate.toLocaleTimeString('fr-CH', { hour: '2-digit', minute: '2-digit' })}
          </span>
        )}
      </div>

      {loading && <p className="departure-board__loading">Chargement…</p>}
      {error && <p className="departure-board__error">{error}</p>}

      {!loading && !error && (
        <table className="departure-board__table">
          <thead>
            <tr>
              <th>Ligne</th>
              <th>Opérateur</th>
              <th>Direction</th>
              <th>Départ</th>
              <th>État</th>
            </tr>
            <tr className="departure-board__filters">
              <th>
                <select
                  value={filters.ligne}
                  onChange={e => setFilter('ligne', e.target.value)}
                  className="departure-board__filter-select"
                  aria-label="Filtrer par ligne"
                >
                  <option value="">Toutes</option>
                  {lignes.map(l => <option key={l} value={l}>{l}</option>)}
                </select>
              </th>
              <th>
                <select
                  value={filters.operator}
                  onChange={e => setFilter('operator', e.target.value)}
                  className="departure-board__filter-select"
                  aria-label="Filtrer par opérateur"
                >
                  <option value="">Tous</option>
                  {operators.map(o => <option key={o} value={o}>{o}</option>)}
                </select>
              </th>
              <th>
                <select
                  value={filters.direction}
                  onChange={e => setFilter('direction', e.target.value)}
                  className="departure-board__filter-select"
                  aria-label="Filtrer par direction"
                >
                  <option value="">Toutes</option>
                  {directions.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
              </th>
              <th />
              <th />
            </tr>
          </thead>
          <tbody>
            {filtered.map((dep, i) => {
              const delay = fmtDelay(dep.delaySeconds)
              const isSelected = selectedDeparture?.tripId === dep.tripId && selectedDeparture?.stopId === dep.stopId
              return (
                <tr
                  key={`${dep.tripId}-${i}`}
                  className={`departure-board__row${isSelected ? ' departure-board__row--selected' : ''}`}
                  onClick={() => onDepartureSelect?.(dep)}
                  title="Cliquer pour voir le trajet sur la carte"
                >
                  <td>
                    <span className="departure-board__line">{dep.routeShortName}</span>
                  </td>
                  <td className="departure-board__operator">{dep.operator}</td>
                  <td className="departure-board__headsign">{dep.headsign}</td>
                  <td className="departure-board__time">{fmtTime(dep.scheduledDeparture)}</td>
                  <td>
                    {delay && (
                      <span className={`departure-board__delay ${delay.cls}`}>
                        {delay.label}
                      </span>
                    )}
                  </td>
                </tr>
              )
            })}
            {filtered.length === 0 && (
              <tr>
                <td colSpan={5} className="departure-board__empty">
                  {departures.length === 0 ? 'Aucun départ prévu' : 'Aucun résultat pour ces filtres'}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  )
}
