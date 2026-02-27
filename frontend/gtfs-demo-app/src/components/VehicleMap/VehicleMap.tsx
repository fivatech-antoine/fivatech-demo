import L from 'leaflet'
import { useCallback, useEffect, useRef, useState } from 'react'
import { MapContainer, Marker, Popup, TileLayer } from 'react-leaflet'
import { getVehicles } from '../../services/api'
import type { Stop, VehiclePosition } from '../../types'
import './VehicleMap.css'

// ── Fix icône Leaflet (problème connu avec Vite) ──────────────────────────────
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({ iconUrl: markerIcon, iconRetinaUrl: markerIcon2x, shadowUrl: markerShadow })

// Centre Lausanne
const LAUSANNE: [number, number] = [46.5197, 6.6323]

function makeVehicleIcon(routeShortName: string, bearing?: number) {
  return L.divIcon({
    html: `<div class="vehicle-icon" style="transform: rotate(${bearing ?? 0}deg)">
             <span class="vehicle-icon__label">${routeShortName || '?'}</span>
           </div>`,
    className: '',
    iconSize: [36, 36],
    iconAnchor: [18, 18],
  })
}

interface Props {
  selectedStop: Stop | null
}

export function VehicleMap({ selectedStop }: Props) {
  const [vehicles, setVehicles] = useState<VehiclePosition[]>([])
  const [error, setError] = useState<string | null>(null)
  const mapRef = useRef<L.Map | null>(null)

  const refresh = useCallback(() => {
    getVehicles()
      .then(data => { setVehicles(data); setError(null) })
      .catch(() => setError('Impossible de charger les positions véhicules'))
  }, [])

  useEffect(() => {
    refresh()
    const id = setInterval(refresh, 30_000)
    return () => clearInterval(id)
  }, [refresh])

  // Recentrer la carte sur l'arrêt sélectionné
  useEffect(() => {
    if (selectedStop && mapRef.current) {
      mapRef.current.flyTo([selectedStop.lat, selectedStop.lon], 16, { duration: 1 })
    }
  }, [selectedStop])

  return (
    <div className="vehicle-map">
      {error && <div className="vehicle-map__error">{error}</div>}

      <MapContainer
        center={LAUSANNE}
        zoom={13}
        className="vehicle-map__map"
        ref={mapRef}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        {/* Marqueur arrêt sélectionné */}
        {selectedStop && (
          <Marker position={[selectedStop.lat, selectedStop.lon]}>
            <Popup>
              <strong>{selectedStop.name}</strong>
              {selectedStop.platformCode && <><br />Quai {selectedStop.platformCode}</>}
            </Popup>
          </Marker>
        )}

        {/* Véhicules */}
        {vehicles.map(v => (
          <Marker
            key={v.vehicleId}
            position={[v.latitude, v.longitude]}
            icon={makeVehicleIcon(v.routeShortName, v.bearing)}
          >
            <Popup>
              <strong>Ligne {v.routeShortName}</strong><br />
              Véhicule : {v.vehicleId}<br />
              {v.speed != null && <>Vitesse : {Math.round(v.speed * 3.6)} km/h<br /></>}
              Statut : {v.currentStatus}
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  )
}
