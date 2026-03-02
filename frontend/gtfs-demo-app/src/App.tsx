import { useEffect, useState } from 'react'
import { StopSearch } from './components/StopSearch/StopSearch'
import { DepartureBoard } from './components/DepartureBoard/DepartureBoard'
import { VehicleMap } from './components/VehicleMap/VehicleMap'
import { getTripStops } from './services/api'
import type { Departure, Stop, TripStop } from './types'

export default function App() {
  const [selectedStop, setSelectedStop] = useState<Stop | null>(null)
  const [selectedDeparture, setSelectedDeparture] = useState<Departure | null>(null)
  const [tripStops, setTripStops] = useState<TripStop[]>([])

  // Réinitialise la sélection de départ quand on change d'arrêt
  function handleStopSelect(stop: Stop) {
    setSelectedStop(stop)
    setSelectedDeparture(null)
    setTripStops([])
  }

  // Charge les arrêts du trajet à chaque nouveau départ sélectionné
  useEffect(() => {
    if (!selectedDeparture) { setTripStops([]); return }
    getTripStops(selectedDeparture.tripId, selectedDeparture.stopId)
      .then(setTripStops)
      .catch(() => setTripStops([]))
  }, [selectedDeparture])

  return (
    <div className="app">
      <header className="app__header">
        <h1 className="app__title">
          <a href="https://www.fivatech.ch" target="blank">
            <span style={{ color: '#E2725B' }}>fiva</span>
            <span style={{ color: '#8A9A5B' }}>tech</span>
          </a>
        </h1>
        <p className="app__subtitle">Transports en commun en Suisse — Prochains départs par station, retards en temps réel</p>
        <div className="app__credits">
          <p>Données : <a href="https://opentransportdata.swiss/fr/" target="blank">Open Transport Data Swiss</a></p>
          <p>Carte : <a href="https://www.openstreetmap.org" target="blank">OpenStreetMap</a></p>
        </div>
      </header>

      <main className="app__main">
        <section className="app__sidebar">
          <StopSearch onStopSelect={handleStopSelect} />
          {selectedStop && (
            <DepartureBoard
              stop={selectedStop}
              selectedDeparture={selectedDeparture}
              onDepartureSelect={setSelectedDeparture}
            />
          )}
        </section>

        <section className="app__map-section">
          <VehicleMap selectedStop={selectedStop} tripStops={tripStops} />
        </section>
      </main>
    </div>
  )
}
