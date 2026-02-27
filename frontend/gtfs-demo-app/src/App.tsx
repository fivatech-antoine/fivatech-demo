import { useState } from 'react'
import { StopSearch } from './components/StopSearch/StopSearch'
import { DepartureBoard } from './components/DepartureBoard/DepartureBoard'
import { VehicleMap } from './components/VehicleMap/VehicleMap'
import type { Stop } from './types'

export default function App() {
  const [selectedStop, setSelectedStop] = useState<Stop | null>(null)

  return (
    <div className="app">
      <header className="app__header">
        <h1 className="app__title">
          <span style={{ color: '#E2725B' }}>fiva</span>
          <span style={{ color: '#8A9A5B' }}>tech</span>
        </h1>
        <p className="app__subtitle">Transports en commun en Suisse — Prochains départs par station, retards en temps réel</p>
      </header>

      <main className="app__main">
        <section className="app__sidebar">
          <StopSearch onStopSelect={setSelectedStop} />
          {selectedStop && <DepartureBoard stop={selectedStop} />}
        </section>

        <section className="app__map-section">
          <VehicleMap selectedStop={selectedStop} />
        </section>
      </main>
    </div>
  )
}
