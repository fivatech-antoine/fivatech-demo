import { useState } from 'react'
import './DisclaimerModal.css'

export function DisclaimerModal() {
  const [visible, setVisible] = useState(true)

  if (!visible) return null

  return (
    <div className="disclaimer-overlay" role="dialog" aria-modal="true">
      <div className="disclaimer-modal">
        <div className="disclaimer-modal__logo">
          <span style={{ color: '#E2725B' }}>fiva</span>
          <span style={{ color: '#8A9A5B' }}>tech</span>
        </div>
        <p className="disclaimer-modal__text">
          Vous accédez à une application de démonstration réalisée par Fivatech ayant pour
          objectif d'illustrer ce qu'il est possible de faire avec l'IA en très peu de
          temps, grâce à la double compétence métier&nbsp;/&nbsp;technique. Cette
          application n'est pas maintenue et Fivatech ne garantit pas l'exactitude des
          informations affichées.
        </p>
        <button className="disclaimer-modal__btn" onClick={() => setVisible(false)}>
          J'ai compris
        </button>
      </div>
    </div>
  )
}
