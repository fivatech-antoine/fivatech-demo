export interface Stop {
  stopId: string
  name: string
  lat: number
  lon: number
  platformCode?: string
}

export interface Departure {
  tripId: string
  routeShortName: string
  headsign: string
  /** ISO 8601 */
  scheduledDeparture: string
  /** ISO 8601, absent si pas de données RT */
  estimatedDeparture?: string
  /** Positif = retard, négatif = avance (secondes) */
  delaySeconds?: number
  stopId: string
  platform?: string
  operator?:string
}

export interface TripStop {
  stopId: string
  name: string
  lat: number
  lon: number
  sequence: number
  /** ISO 8601 */
  scheduledDeparture: string
}
