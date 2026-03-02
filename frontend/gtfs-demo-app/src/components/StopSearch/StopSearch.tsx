import { useEffect, useRef, useState } from 'react'
import { searchStops } from '../../services/api'
import type { Stop } from '../../types'
import './StopSearch.css'

interface Props {
  onStopSelect: (stop: Stop) => void
}

function useDebounce<T>(value: T, ms: number): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), ms)
    return () => clearTimeout(id)
  }, [value, ms])
  return debounced
}

export function StopSearch({ onStopSelect }: Props) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<Stop[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const skipNextSearch = useRef(false)

  const debouncedQuery = useDebounce(query, 400)

  useEffect(() => {
    if (skipNextSearch.current) {
      skipNextSearch.current = false
      return
    }
    if (debouncedQuery.trim().length < 2) {
      setResults([])
      return
    }
    setLoading(true)
    setError(null)
    searchStops(debouncedQuery)
      .then(stops => { setResults(stops); setLoading(false) })
      .catch(() => { setError('Erreur de recherche'); setLoading(false) })
  }, [debouncedQuery])

  const handleSelect = (stop: Stop) => {
    skipNextSearch.current = true
    setQuery(stop.name)
    setResults([])
    onStopSelect(stop)
  }

  return (
    <div className="stop-search">
      <div className="stop-search__input-wrap">
        <span className="stop-search__icon" aria-hidden>🔍</span>
        <input
          className="stop-search__input"
          type="text"
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Rechercher un arrêt…"
          aria-label="Rechercher un arrêt"
        />
        {loading && <span className="stop-search__spinner" aria-hidden />}
      </div>

      {error && <p className="stop-search__error">{error}</p>}

      {results.length > 0 && (
        <ul className="stop-search__results" role="listbox">
          {results.map(stop => (
            <li
              key={stop.stopId}
              className="stop-search__item"
              role="option"
              aria-selected={false}
              onClick={() => handleSelect(stop)}
            >
              <span className="stop-search__name">{stop.name}</span>
              {stop.platformCode && (
                <span className="stop-search__platform">Quai {stop.platformCode}</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
