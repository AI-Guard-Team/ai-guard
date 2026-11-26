import { Lock, RotateCcw } from 'lucide-react'
import { useCallback, useState, type FormEvent } from 'react'
import { getRiskSettings } from '../../api/admin'
import { ApiError } from '../../api/client'
import { getExamRiskSettings, resetExamRiskSettings, updateExamRiskSettings } from '../../api/management'
import { Alert } from '../../components/Alert'
import { Button } from '../../components/Button'
import { InfoTip } from '../../components/InfoTip'
import { Modal } from '../../components/Modal'
import { Skeleton } from '../../components/Skeleton'
import { useToast } from '../../components/useToast'
import { useResource } from '../../hooks/useResource'
import { common } from '../../i18n/common'
import { getLang } from '../../i18n/lang'
import { defineMessages, useMessages } from '../../i18n/messages'
import { eventLabel } from '../../lib/eventCatalog'
import { formatNumber } from '../../lib/format'
import { hasRiskErrors, parseNumber, RISK_LIMITS, ruleDiffers, toRiskDraft, toRiskRequest, validateRisk, type RiskDraft, type RiskErrorCode, type RuleDraft } from './riskForm'

const messages = defineMessages({
  tr: {
    failed: 'Risk ayarları yüklenemedi.',
    title: 'Risk ayarları',
    how: 'Skor nasıl hesaplanır?',
    howItems: [
      'Her olay türünün bir ağırlığı var; olay süresi uzadıkça ve aynı tür tekrarlandıkça katkı artar.',
      'Bazı olayların katkısı zamanla üstel olarak azalır (dakika başına katsayı).',
      'Oturum skoru eşiği geçince oturum "riskli" işaretlenir ve değerlendiriciye düşer. Kararı her zaman insan verir.',
    ],
    sub: 'Varsayılan değerler bütün sınavlar için aynı. Bu sınava özel değer girerseniz yalnızca bu sınavın skorları onunla hesaplanır.',
    locked: 'Bu sınavda oturum açıldı; ayarlar kilitli. Aynı sınavdaki bütün adaylar aynı değerlerle puanlanır.',
    threshold: 'Oturum eşiği',
    step: 'Tekrar başına artış',
    maxMultiplier: 'En fazla tekrar çarpanı',
    platformWide: 'Platform geneli',
    event: 'Olay',
    weight: 'Ağırlık',
    decay: 'Azalma / dk',
    single: 'Tek başına eşik',
    none: 'Yok',
    custom: 'Bu sınava özel',
    defaultValue: (value: string) => `Varsayılan ${value}`,
    weightLabel: (event: string) => `${event}: ağırlık`,
    decayLabel: (event: string) => `${event}: dakika başına azalma`,
    singleLabel: (event: string) => `${event}: tek başına riskli eşiği`,
    errors: {
      weight: `Ağırlık 0–${RISK_LIMITS.weight}`,
      decay: `Azalma 0–${RISK_LIMITS.decay}`,
      threshold: `Eşik boş ya da 0–${RISK_LIMITS.threshold}`,
      session: `Oturum eşiği 1–${RISK_LIMITS.session} olmalı.`,
    } as Record<RiskErrorCode, string>,
    fix: 'İşaretli değerleri düzeltin.',
    saved: 'Risk ayarları kaydedildi.',
    unchanged: 'Değişiklik yok.',
    customCount: (n: number) => (n === 0 ? 'Bütün değerler varsayılan.' : `${n} değer bu sınava özel.`),
    reset: 'Varsayılana dön',
    resetTitle: 'Varsayılan değerlere dönülsün mü?',
    resetBody: 'Bu sınava özel bütün risk değerleri silinir; sınav platformun varsayılan değerlerini kullanır.',
    resetDone: 'Varsayılan değerlere dönüldü.',
  },
  en: {
    failed: 'Risk settings could not be loaded.',
    title: 'Risk settings',
    how: 'How is the score calculated?',
    howItems: [
      'Each event type has a weight; its contribution grows as the event lasts longer and the same type repeats.',
      'The contribution of some events decays exponentially over time (a per-minute factor).',
      'When the session score passes the threshold, the session is marked "risky" and goes to a reviewer. A person always makes the decision.',
    ],
    sub: 'The default values are the same for every exam. If you enter exam-specific values, only this exam’s scores use them.',
    locked: 'A session has opened in this exam, so the settings are locked. Every candidate in the same exam is scored with the same values.',
    threshold: 'Session threshold',
    step: 'Increase per repetition',
    maxMultiplier: 'Maximum repetition multiplier',
    platformWide: 'Platform-wide',
    event: 'Event',
    weight: 'Weight',
    decay: 'Decay / min',
    single: 'Risky on its own at',
    none: 'None',
    custom: 'Exam-specific',
    defaultValue: (value: string) => `Default ${value}`,
    weightLabel: (event: string) => `${event}: weight`,
    decayLabel: (event: string) => `${event}: decay per minute`,
    singleLabel: (event: string) => `${event}: risky-on-its-own threshold`,
    errors: {
      weight: `Weight 0–${RISK_LIMITS.weight}`,
      decay: `Decay 0–${RISK_LIMITS.decay}`,
      threshold: `Threshold empty or 0–${RISK_LIMITS.threshold}`,
      session: `The session threshold must be 1–${RISK_LIMITS.session}.`,
    },
    fix: 'Fix the highlighted values.',
    saved: 'Risk settings saved.',
    unchanged: 'Nothing changed.',
    customCount: (n: number) => (n === 0 ? 'All values are the defaults.' : `${n} ${n === 1 ? 'value is' : 'values are'} specific to this exam.`),
    reset: 'Back to defaults',
    resetTitle: 'Go back to the default values?',
    resetBody: 'All exam-specific risk values are removed; the exam uses the platform defaults.',
    resetDone: 'Back to the default values.',
  },
})

interface Props {
  examId: string
}

const decimal = () => (getLang() === 'tr' ? ',' : '.')

/**
 * Risk ayarları (Stitch 29; `GET/PUT/DELETE /api/exams/{id}/risk-settings`, K-49). Varsayılan riskconfig.json;
 * sınav yalnızca farklı olan değerleri saklar. İlk oturum açılınca kilitli (sunucu 409 döner, ekran salt okunur).
 */
export function RiskTab({ examId }: Props) {
  const m = useMessages(messages)
  const c = useMessages(common)
  const showToast = useToast()
  const load = useCallback(
    async (signal: AbortSignal) => {
      const [exam, platform] = await Promise.all([getExamRiskSettings(examId, signal), getRiskSettings(signal)])
      return { exam, platform }
    },
    [examId],
  )
  const { data, error, reload } = useResource(load, m.failed)
  const [draft, setDraft] = useState<RiskDraft | null>(null)
  const [submitted, setSubmitted] = useState(false)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [confirmReset, setConfirmReset] = useState(false)

  // Sunucudan yeni hâl gelince (ilk yükleme, kayıt, sıfırlama) taslak ona döner.
  const [source, setSource] = useState(data)
  if (data && source !== data) {
    setSource(data)
    setDraft(toRiskDraft(data.exam, decimal()))
  }

  const settings = data?.exam
  const errors = draft ? validateRisk(draft) : { rules: {} }
  const shown = submitted ? errors : { rules: {} as Record<string, RiskErrorCode[]> }
  const request = draft && settings && !hasRiskErrors(errors) ? toRiskRequest(draft, settings) : null
  const customCount = draft && settings ? draft.rules.filter((rule) => ruleDiffers(rule, settings)).length + (parseNumber(draft.sessionThreshold) !== settings.defaultSessionThreshold ? 1 : 0) : 0
  const dirty = draft && settings ? JSON.stringify(draft) !== JSON.stringify(toRiskDraft(settings, decimal())) : false
  const maxWeight = Math.max(1, ...(draft?.rules.map((rule) => parseNumber(rule.weight) || 0) ?? [1]))

  const setRule = (type: string, patch: Partial<RuleDraft>) =>
    setDraft((current) => (current ? { ...current, rules: current.rules.map((rule) => (rule.type === type ? { ...rule, ...patch } : rule)) } : current))

  const save = async (event: FormEvent) => {
    event.preventDefault()
    setSubmitted(true)
    setFormError(null)
    if (!draft || !settings) return
    if (hasRiskErrors(errors) || !request) {
      setFormError(m.fix)
      return
    }
    if (!dirty) {
      showToast(m.unchanged, 'info')
      return
    }
    setSaving(true)
    try {
      await updateExamRiskSettings(examId, request)
      showToast(m.saved, 'success')
      setSubmitted(false)
      reload()
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.message : m.failed)
      if (caught instanceof ApiError && caught.status === 409) reload()
    } finally {
      setSaving(false)
    }
  }

  const reset = async () => {
    setSaving(true)
    try {
      await resetExamRiskSettings(examId)
      showToast(m.resetDone, 'success')
      setConfirmReset(false)
      reload()
    } catch (caught) {
      setFormError(caught instanceof ApiError ? caught.message : m.failed)
      setConfirmReset(false)
    } finally {
      setSaving(false)
    }
  }

  const locked = settings?.locked ?? false
  const savedCustom = settings ? settings.events.some((rule) => rule.customized) || settings.sessionThreshold !== settings.defaultSessionThreshold : false

  return (
    <div className="editor-card">
      <div className="editor-card-head">
        <div>
          <h2 className="editor-card-title">
            {m.title}{' '}
            <InfoTip title={m.how}>
              <ul>
                {m.howItems.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </InfoTip>
          </h2>
          <p className="editor-card-sub">{m.sub}</p>
        </div>
      </div>

      {locked ? (
        <p className="risk-locked">
          <Lock size={16} aria-hidden="true" />
          {m.locked}
        </p>
      ) : null}

      {error && !data ? (
        <Alert tone="danger">
          {error}{' '}
          <Button variant="ghost" onClick={reload}>
            {c.retry}
          </Button>
        </Alert>
      ) : !data || !draft || !settings ? (
        <Skeleton height="20rem" />
      ) : (
        <form className="risk-form" onSubmit={save} noValidate>
          <dl className="risk-facts">
            <div className={parseNumber(draft.sessionThreshold) !== settings.defaultSessionThreshold ? 'is-custom' : undefined}>
              <dt>
                <label htmlFor="risk-session">{m.threshold}</label>
              </dt>
              <dd>
                <input
                  id="risk-session"
                  className="risk-input risk-input-lg"
                  inputMode="decimal"
                  value={draft.sessionThreshold}
                  disabled={locked}
                  aria-invalid={shown.session ? true : undefined}
                  onChange={(event) => setDraft({ ...draft, sessionThreshold: event.target.value })}
                />
              </dd>
              <span className="risk-default">{m.defaultValue(formatNumber(settings.defaultSessionThreshold))}</span>
            </div>
            <div>
              <dt>{m.step}</dt>
              <dd>+{formatNumber(data.platform.repetitionStep, 2)}</dd>
              <span className="risk-default">{m.platformWide}</span>
            </div>
            <div>
              <dt>{m.maxMultiplier}</dt>
              <dd>×{formatNumber(data.platform.maxRepetitionMultiplier, 1)}</dd>
              <span className="risk-default">{m.platformWide}</span>
            </div>
          </dl>
          {shown.session ? (
            <p className="field-error" role="alert">
              {m.errors.session}
            </p>
          ) : null}

          <div className="table-scroll">
            <table className="data-table risk-table">
              <thead>
                <tr>
                  <th scope="col">{m.event}</th>
                  <th scope="col">{m.weight}</th>
                  <th scope="col">{m.decay}</th>
                  <th scope="col">{m.single}</th>
                </tr>
              </thead>
              <tbody>
                {draft.rules.map((rule) => {
                  const base = settings.events.find((item) => item.type === rule.type)!
                  const label = eventLabel(rule.type)
                  const ruleErrors = shown.rules[rule.type] ?? []
                  const differs = ruleDiffers(rule, settings)
                  const weight = parseNumber(rule.weight)
                  return (
                    <tr key={rule.type} className={differs ? 'is-custom' : undefined}>
                      <td>
                        <strong>{label}</strong>
                        {differs ? <span className="risk-chip">{m.custom}</span> : null}
                        {ruleErrors.length ? (
                          <span className="field-error risk-row-error" role="alert">
                            {ruleErrors.map((code) => m.errors[code]).join(' · ')}
                          </span>
                        ) : null}
                      </td>
                      <td>
                        <span className="weight-cell">
                          <input
                            className="risk-input"
                            inputMode="decimal"
                            aria-label={m.weightLabel(label)}
                            value={rule.weight}
                            disabled={locked}
                            aria-invalid={ruleErrors.includes('weight') ? true : undefined}
                            onChange={(event) => setRule(rule.type, { weight: event.target.value })}
                          />
                          <span className="weight-bar" aria-hidden="true">
                            <span style={{ width: `${Number.isFinite(weight) ? Math.min(100, (weight / maxWeight) * 100) : 0}%` }} />
                          </span>
                        </span>
                        <span className="risk-default">{m.defaultValue(formatNumber(base.defaultWeight, 1))}</span>
                      </td>
                      <td>
                        <input
                          className="risk-input"
                          inputMode="decimal"
                          aria-label={m.decayLabel(label)}
                          value={rule.decay}
                          disabled={locked}
                          aria-invalid={ruleErrors.includes('decay') ? true : undefined}
                          onChange={(event) => setRule(rule.type, { decay: event.target.value })}
                        />
                        <span className="risk-default">{m.defaultValue(base.defaultDecayPerMinute > 0 ? formatNumber(base.defaultDecayPerMinute, 3) : m.none)}</span>
                      </td>
                      <td>
                        <input
                          className="risk-input"
                          inputMode="decimal"
                          placeholder={m.none}
                          aria-label={m.singleLabel(label)}
                          value={rule.threshold}
                          disabled={locked}
                          aria-invalid={ruleErrors.includes('threshold') ? true : undefined}
                          onChange={(event) => setRule(rule.type, { threshold: event.target.value })}
                        />
                        <span className="risk-default">{m.defaultValue(base.defaultThreshold !== null ? formatNumber(base.defaultThreshold) : m.none)}</span>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>

          {formError ? <Alert tone="danger">{formError}</Alert> : null}

          {locked ? null : (
            <div className="risk-actions">
              <span className="admin-row-sub">{m.customCount(customCount)}</span>
              <span className="risk-actions-buttons">
                {savedCustom ? (
                  <Button type="button" variant="ghost" disabled={saving} onClick={() => setConfirmReset(true)}>
                    <RotateCcw size={16} aria-hidden="true" /> {m.reset}
                  </Button>
                ) : null}
                {dirty ? (
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={saving}
                    onClick={() => {
                      setDraft(toRiskDraft(settings, decimal()))
                      setSubmitted(false)
                      setFormError(null)
                    }}
                  >
                    {c.cancel}
                  </Button>
                ) : null}
                <Button type="submit" disabled={saving || !dirty}>
                  {saving ? c.saving : c.save}
                </Button>
              </span>
            </div>
          )}
        </form>
      )}

      <Modal open={confirmReset} title={m.resetTitle} onClose={() => setConfirmReset(false)}>
        <p>{m.resetBody}</p>
        <div className="modal-actions">
          <Button variant="secondary" onClick={() => setConfirmReset(false)}>
            {c.cancel}
          </Button>
          <Button variant="danger" loading={saving} onClick={reset}>
            {m.reset}
          </Button>
        </div>
      </Modal>
    </div>
  )
}
