import type { Departure, Stop, TripStop, VehiclePosition } from '../types'

const BASE = '/api'

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url)
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(`[${res.status}] ${text}`)
  }
  return res.json() as Promise<T>
}

export const searchStops = (query: string): Promise<Stop[]> =>
  fetchJson(`${BASE}/stops?query=${encodeURIComponent(query)}`)

export const getStop = (stopId: string): Promise<Stop> =>
  fetchJson(`${BASE}/stops/${encodeURIComponent(stopId)}`)

export const getDepartures = (stopId: string, limit = 20): Promise<Departure[]> =>
  fetchJson(`${BASE}/departures/${encodeURIComponent(stopId)}?limit=${limit}`)

export const getVehicles = (): Promise<VehiclePosition[]> =>
  fetchJson(`${BASE}/vehicles`)

export const getTripStops = (tripId: string, fromStopId: string): Promise<TripStop[]> =>
  fetchJson(`${BASE}/trips/${encodeURIComponent(tripId)}/stops?fromStopId=${encodeURIComponent(fromStopId)}`)
