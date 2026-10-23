import { useState, type FormEvent } from 'react'
import './App.css'

type Status = 'pass' | 'warn' | 'fail'

type CheckResult = {
  domain: string
  mx: { status: Status; records: string[] }
  spf: { status: Status; record: string | null }
  dmarc: { status: Status; policy: string | null; record: string | null }
}

const API_URL = 'http://localhost:5174' // changes when we deploy

function App() {
  const [domain, setDomain] = useState('')
  const [result, setResult] = useState<CheckResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function check(e: FormEvent) {
    e.preventDefault()                // stop the page from reloading
    if (!domain.trim()) return

    setLoading(true)
    setError(null)
    setResult(null)

    try {
      const res = await fetch(`${API_URL}/api/dns/${encodeURIComponent(domain.trim())}`)
      const data = await res.json().catch(() => null)

      if (!res.ok) {
        setError(data?.error ?? 'Something went wrong.')   // e.g. your 400 message
        return
      }
      setResult(data)
    } catch {
      setError("Couldn't reach the API. Is it running?")
    } finally {
      setLoading(false)
    }
  }

  return (
      <main>
        <h1>Email DNS Checker</h1>

        <form onSubmit={check}>
          <input
              value={domain}
              onChange={e => setDomain(e.target.value)}
              placeholder="example.com"
              required
          />
          <button disabled={loading}>{loading ? 'Checking…' : 'Check'}</button>
        </form>

        {error && <p className="error">{error}</p>}

        {result && (
            <div className="results">
              <Row label="MX" status={result.mx.status}
                   detail={result.mx.records.join(', ') || 'No MX records'} />
              <Row label="SPF" status={result.spf.status}
                   detail={result.spf.record ?? 'No SPF record'} />
              <Row label="DMARC" status={result.dmarc.status}
                   detail={result.dmarc.record ?? 'No DMARC record'} />
            </div>
        )}
      </main>
  )
}

function Row({ label, status, detail }: { label: string; status: Status; detail: string }) {
  return (
      <div className="row">
        <span className="label">{label}</span>
        <span className={`badge ${status}`}>{status}</span>
        <code className="detail">{detail}</code>
      </div>
  )
}

export default App