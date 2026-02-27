# TL Dashboard — GTFS Demo

Dashboard temps réel pour les Transports Lausannois (TL), construit avec **ASP.NET Core (.NET 8)** et **React/Vite/TypeScript**.

## Architecture

```
gtfsdemo/
├── backend/GTFSDemo.Api/       # API REST ASP.NET Core
│   ├── Controllers/            # Stops, Departures, Vehicles
│   ├── Services/
│   │   ├── GtfsStaticService   # Charge & indexe le GTFS statique (ZIP) au démarrage
│   │   ├── GtfsRealtimeService # Refresh GTFS-RT toutes les 30 s (BackgroundService)
│   │   └── CacheService        # Wrapper IMemoryCache
│   ├── Models/                 # Stop, Departure, VehiclePosition
│   ├── Configuration/          # Options OpenTransportData
│   └── Protos/                 # gtfs-realtime.proto → classes C# (Google.Protobuf)
└── frontend/gtfs-demo-app/     # React + Vite + TypeScript
    └── src/
        ├── components/
        │   ├── StopSearch/     # Recherche d'arrêts avec debounce
        │   ├── DepartureBoard/ # Tableau des prochains départs (refresh 30 s)
        │   └── VehicleMap/     # Carte Leaflet des véhicules (refresh 30 s)
        ├── services/api.ts     # Appels fetch vers le backend
        └── types/index.ts      # Types TS alignés sur les models .NET
```

## Prérequis

- .NET 8 SDK
- Node.js ≥ 20
- Clé API [opentransportdata.swiss](https://opentransportdata.swiss)

## Configuration

Éditez `backend/GTFSDemo.Api/appsettings.json` :

```json
{
  "OpenTransportData": {
    "ApiKey": "<votre_clé_api>",
    "GtfsStaticUrl": "<url_du_zip_gtfs_tl>",
    "GtfsRtVehiclePositionsUrl": "<url_vehicle_positions>",
    "GtfsRtTripUpdatesUrl": "<url_trip_updates>"
  }
}
```

## Démarrage

```bash
# Backend (port 5000)
cd backend/GTFSDemo.Api
dotnet run

# Frontend (port 5173, proxy → :5000)
cd frontend/gtfs-demo-app
npm install
npm run dev
```

Ouvrez [http://localhost:5173](http://localhost:5173).

## API Endpoints

| Méthode | Route | Description |
|---------|-------|-------------|
| GET | `/api/stops?query=flon` | Recherche d'arrêts |
| GET | `/api/stops/{stopId}` | Arrêt par ID |
| GET | `/api/departures/{stopId}` | Prochains départs (statique + RT) |
| GET | `/api/vehicles` | Positions de tous les véhicules |
| GET | `/api/vehicles/{tripId}` | Position d'un véhicule |

Swagger UI disponible en dev : [http://localhost:5000/swagger](http://localhost:5000/swagger)

## Notes techniques

- **GTFS statique** : téléchargé et mis en cache localement 12h (`%TEMP%/gtfsdemo_static.zip`). Les index sont construits en mémoire au démarrage.
- **GTFS-RT** : rafraîchi toutes les 30 s via `BackgroundService`. Nécessite une clé API valide.
- **Temps GTFS > 24h** : les services passant minuit (ex. `25:30:00`) sont correctement normalisés.
- **CORS** : configuré pour le dev server Vite (`localhost:5173`).
