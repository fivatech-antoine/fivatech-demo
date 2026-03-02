import L from 'leaflet'
import { useEffect, useRef } from 'react'
import { CircleMarker, MapContainer, Marker, Polyline, Popup, TileLayer } from 'react-leaflet'
import type { Stop, TripStop } from '../../types'
import './VehicleMap.css'

// ── Fix icône Leaflet (problème connu avec Vite) ──────────────────────────────
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({ iconUrl: markerIcon, iconRetinaUrl: markerIcon2x, shadowUrl: markerShadow })

// Centre Lausanne
const LAUSANNE: [number, number] = [46.5197, 6.6323]

interface Props {
  selectedStop: Stop | null
  tripStops?: TripStop[]
}

export function VehicleMap({ selectedStop, tripStops = [] }: Props) {
  const mapRef = useRef<L.Map | null>(null)

  // Recentrer la carte sur l'arrêt sélectionné
  useEffect(() => {
    if (selectedStop && mapRef.current) {
      mapRef.current.flyTo([selectedStop.lat, selectedStop.lon], 16, { duration: 1 })
    }
  }, [selectedStop])

  // Ajuster les limites pour afficher tout le trajet sélectionné
  useEffect(() => {
    if (tripStops.length > 0 && mapRef.current) {
      const bounds = L.latLngBounds(tripStops.map(s => [s.lat, s.lon] as [number, number]))
      mapRef.current.fitBounds(bounds, { padding: [40, 40], maxZoom: 16 })
    }
  }, [tripStops])

  return (
    <div className="vehicle-map">
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

        {/* Trajet sélectionné — tracé de la ligne */}
        {tripStops.length > 1 && (
          <Polyline
            positions={tripStops.map(s => [s.lat, s.lon] as [number, number])}
            color="#3a6fd8"
            weight={4}
            opacity={0.8}
          />
        )}

        {/* Trajet sélectionné — arrêts */}
        {tripStops.map((s, i) => (
          <CircleMarker
            key={s.stopId}
            center={[s.lat, s.lon]}
            radius={i === 0 ? 7 : 5}
            pathOptions={{
              color: '#3a6fd8',
              fillColor: i === 0 ? '#3a6fd8' : '#fff',
              fillOpacity: 1,
              weight: 2,
            }}
          >
            <Popup>
              <strong>{s.name}</strong><br />
              {new Date(s.scheduledDeparture).toLocaleTimeString('fr-CH', { hour: '2-digit', minute: '2-digit' })}
            </Popup>
          </CircleMarker>
        ))}
      </MapContainer>
    </div>
  )
}
