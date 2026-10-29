import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api, ApiError } from '../api/client';
import type { Catalogue, Claim, QuoteDetail, Risk, SectionCode } from '../api/types';
import { useUser } from '../App';
import { today } from '../format';

const emptyRisk: Risk = {
  insured: { name: '', addressLine1: '', town: '', postcode: '', tradeCode: '', yearsTrading: 1, turnover: 0, contactEmail: '' },
  premises: { construction: 'Standard', roof: 'Standard', yearBuilt: 1990, occupied: true, listedBuilding: false, floodInLast10Years: false, alarm: 'None' },
  materialDamage: { buildings: 0, contents: 0, stock: 0, computerEquipment: 0, excess: 250 },
  businessInterruption: null,
  employersLiability: { employees: 1, wageroll: 0, manualWageroll: 0 },
  publicLiability: { limitOfIndemnity: 2000000, productsSupplied: false, workAwayFromPremises: false },
  claims: [],
  previouslyDeclinedOrCancelled: false
};

type Errors = Record<string, string[]>;

export function QuoteFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const user = useUser();
  const [catalogue, setCatalogue] = useState<Catalogue>();
  const [risk, setRisk] = useState<Risk>(emptyRisk);
  const [inception, setInception] = useState(today(7));
  const [brokerReference, setBrokerReference] = useState('');
  const [commission, setCommission] = useState<string>('');
  const [errors, setErrors] = useState<Errors>({});
  const [busy, setBusy] = useState(false);

  useEffect(() => { api.get<Catalogue>('/api/catalogue').then(setCatalogue); }, []);
  useEffect(() => {
    if (!id) return;
    api.get<QuoteDetail>(`/api/quotes/${id}`).then(q => {
      setRisk(q.risk);
      setInception(q.inceptionDate);
      setBrokerReference(q.brokerReference ?? '');
      setCommission(String(q.commissionRate * 100));
    });
  }, [id]);

  const set = <K extends keyof Risk>(key: K, value: Risk[K]) => setRisk(r => ({ ...r, [key]: value }));
  const setInsured = (patch: Partial<Risk['insured']>) => set('insured', { ...risk.insured, ...patch });
  const setPremises = (patch: Partial<Risk['premises']>) => set('premises', { ...risk.premises, ...patch });
  const num = (v: string) => (v === '' ? 0 : Number(v));
  const err = (field: string) => errors[field]?.map(e => <span key={e} className="field-error">{e}</span>);

  const toggle = (section: 'materialDamage' | 'businessInterruption' | 'employersLiability' | 'publicLiability', on: boolean) =>
    set(section, on ? (emptyRisk[section] ?? { grossProfit: 0, indemnityPeriodMonths: 12 }) as never : null);

  const addClaim = () => set('claims', [...risk.claims, { date: today(-365), section: 'MaterialDamage', amount: 0, description: '', open: false }]);
  const setClaim = (i: number, patch: Partial<Claim>) => set('claims', risk.claims.map((c, j) => (j === i ? { ...c, ...patch } : c)));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErrors({});
    const body = {
      inceptionDate: inception,
      brokerReference: brokerReference || null,
      commissionRate: commission === '' ? null : Number(commission) / 100,
      risk
    };
    try {
      const result = id
        ? await api.put<{ id: string }>(`/api/quotes/${id}`, body)
        : await api.post<{ id: string }>('/api/quotes', body);
      navigate(`/quotes/${result.id}`);
    } catch (ex) {
      if (ex instanceof ApiError) setErrors(ex.problem.errors ?? { '': [ex.message] });
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit} className="quote-form">
      <h1>{id ? 'Change and requote' : 'New Business Combined quote'}</h1>
      {errors[''] && <div className="error">{errors['']}</div>}
      {errors['sections'] && <div className="error">{errors['sections']}</div>}

      <fieldset>
        <legend>Quote</legend>
        <label>Inception date<input type="date" value={inception} onChange={e => setInception(e.target.value)} />{err('inceptionDate')}</label>
        <label>Your reference<input value={brokerReference} onChange={e => setBrokerReference(e.target.value)} /></label>
        <label>Commission % (agency default if empty, {user.agency})<input type="number" step="0.5" value={commission} onChange={e => setCommission(e.target.value)} />{err('commissionRate')}</label>
      </fieldset>

      <fieldset>
        <legend>Proposer</legend>
        <label>Business name<input value={risk.insured.name} onChange={e => setInsured({ name: e.target.value })} />{err('insured.name')}</label>
        <label>Address<input value={risk.insured.addressLine1} onChange={e => setInsured({ addressLine1: e.target.value })} />{err('insured.addressLine1')}</label>
        <label>Town<input value={risk.insured.town} onChange={e => setInsured({ town: e.target.value })} />{err('insured.town')}</label>
        <label>Postcode<input value={risk.insured.postcode} onChange={e => setInsured({ postcode: e.target.value.toUpperCase() })} />{err('insured.postcode')}</label>
        <label>Trade
          <select value={risk.insured.tradeCode} onChange={e => setInsured({ tradeCode: e.target.value })}>
            <option value="">Select the trade</option>
            {catalogue?.trades.map(t => <option key={t.code} value={t.code}>{t.group}: {t.description} ({t.code})</option>)}
          </select>{err('insured.tradeCode')}
        </label>
        <label>Years trading<input type="number" value={risk.insured.yearsTrading} onChange={e => setInsured({ yearsTrading: num(e.target.value) })} /></label>
        <label>Annual turnover<input type="number" value={risk.insured.turnover} onChange={e => setInsured({ turnover: num(e.target.value) })} />{err('insured.turnover')}</label>
        <label className="check"><input type="checkbox" checked={risk.previouslyDeclinedOrCancelled} onChange={e => set('previouslyDeclinedOrCancelled', e.target.checked)} /> Insurance previously declined, cancelled or special terms imposed</label>
      </fieldset>

      <fieldset>
        <legend>Premises</legend>
        <label>Construction
          <select value={risk.premises.construction} onChange={e => setPremises({ construction: e.target.value as Risk['premises']['construction'] })}>
            <option value="Standard">Brick, stone or concrete</option>
            <option value="TimberFrame">Timber frame</option>
            <option value="NonStandard">Other / non-standard</option>
          </select>
        </label>
        <label>Roof
          <select value={risk.premises.roof} onChange={e => setPremises({ roof: e.target.value as Risk['premises']['roof'] })}>
            <option value="Standard">Slate or tile</option>
            <option value="FlatFelt">Flat felt</option>
            <option value="Thatch">Thatch</option>
            <option value="Other">Other</option>
          </select>
        </label>
        <label>Year built<input type="number" value={risk.premises.yearBuilt} onChange={e => setPremises({ yearBuilt: num(e.target.value) })} />{err('premises.yearBuilt')}</label>
        <label>Intruder alarm
          <select value={risk.premises.alarm} onChange={e => setPremises({ alarm: e.target.value as Risk['premises']['alarm'] })}>
            <option value="None">None</option>
            <option value="Audible">Audible only</option>
            <option value="RemoteSignalling">Remote signalling (NSI/SSAIB)</option>
          </select>
        </label>
        <label className="check"><input type="checkbox" checked={risk.premises.occupied} onChange={e => setPremises({ occupied: e.target.checked })} /> Premises occupied</label>
        <label className="check"><input type="checkbox" checked={risk.premises.listedBuilding} onChange={e => setPremises({ listedBuilding: e.target.checked })} /> Listed building</label>
        <label className="check"><input type="checkbox" checked={risk.premises.floodInLast10Years} onChange={e => setPremises({ floodInLast10Years: e.target.checked })} /> Flooded in the last 10 years</label>
      </fieldset>

      <fieldset>
        <legend><label className="check"><input type="checkbox" checked={!!risk.materialDamage} onChange={e => toggle('materialDamage', e.target.checked)} /> Material damage</label></legend>
        {risk.materialDamage && (
          <>
            {(['buildings', 'contents', 'stock', 'computerEquipment'] as const).map(f => (
              <label key={f}>{f === 'computerEquipment' ? 'Computer equipment' : f[0].toUpperCase() + f.slice(1)}
                <input type="number" value={risk.materialDamage![f]} onChange={e => set('materialDamage', { ...risk.materialDamage!, [f]: num(e.target.value) })} />
              </label>
            ))}
            <label>Excess
              <select value={risk.materialDamage.excess} onChange={e => set('materialDamage', { ...risk.materialDamage!, excess: num(e.target.value) })}>
                {[100, 250, 500, 1000].map(x => <option key={x} value={x}>{x}</option>)}
              </select>
            </label>
            {err('materialDamage')}
          </>
        )}
      </fieldset>

      <fieldset>
        <legend><label className="check"><input type="checkbox" checked={!!risk.businessInterruption} onChange={e => toggle('businessInterruption', e.target.checked)} /> Business interruption</label></legend>
        {risk.businessInterruption && (
          <>
            <label>Gross profit<input type="number" value={risk.businessInterruption.grossProfit} onChange={e => set('businessInterruption', { ...risk.businessInterruption!, grossProfit: num(e.target.value) })} />{err('businessInterruption.grossProfit')}</label>
            <label>Indemnity period (months)
              <select value={risk.businessInterruption.indemnityPeriodMonths} onChange={e => set('businessInterruption', { ...risk.businessInterruption!, indemnityPeriodMonths: num(e.target.value) })}>
                {[12, 18, 24, 36].map(x => <option key={x} value={x}>{x}</option>)}
              </select>
            </label>
            {err('businessInterruption')}
          </>
        )}
      </fieldset>

      <fieldset>
        <legend><label className="check"><input type="checkbox" checked={!!risk.employersLiability} onChange={e => toggle('employersLiability', e.target.checked)} /> Employers' liability</label></legend>
        {risk.employersLiability && (
          <>
            <label>Employees<input type="number" value={risk.employersLiability.employees} onChange={e => set('employersLiability', { ...risk.employersLiability!, employees: num(e.target.value) })} />{err('employersLiability.employees')}</label>
            <label>Total wageroll<input type="number" value={risk.employersLiability.wageroll} onChange={e => set('employersLiability', { ...risk.employersLiability!, wageroll: num(e.target.value) })} />{err('employersLiability.wageroll')}</label>
            <label>Of which manual work<input type="number" value={risk.employersLiability.manualWageroll} onChange={e => set('employersLiability', { ...risk.employersLiability!, manualWageroll: num(e.target.value) })} />{err('employersLiability.manualWageroll')}</label>
          </>
        )}
      </fieldset>

      <fieldset>
        <legend><label className="check"><input type="checkbox" checked={!!risk.publicLiability} onChange={e => toggle('publicLiability', e.target.checked)} /> Public and products liability</label></legend>
        {risk.publicLiability && (
          <>
            <label>Limit of indemnity
              <select value={risk.publicLiability.limitOfIndemnity} onChange={e => set('publicLiability', { ...risk.publicLiability!, limitOfIndemnity: num(e.target.value) })}>
                {(catalogue?.publicLiabilityLimits ?? [1000000, 2000000, 5000000]).map(x => <option key={x} value={x}>{x / 1000000} million</option>)}
              </select>
            </label>
            <label className="check"><input type="checkbox" checked={risk.publicLiability.productsSupplied} onChange={e => set('publicLiability', { ...risk.publicLiability!, productsSupplied: e.target.checked })} /> Products supplied</label>
            <label className="check"><input type="checkbox" checked={risk.publicLiability.workAwayFromPremises} onChange={e => set('publicLiability', { ...risk.publicLiability!, workAwayFromPremises: e.target.checked })} /> Work away from the premises</label>
          </>
        )}
      </fieldset>

      <fieldset>
        <legend>Claims in the last five years</legend>
        {risk.claims.map((c, i) => (
          <div className="claim" key={i}>
            <input type="date" value={c.date} onChange={e => setClaim(i, { date: e.target.value })} />
            <select value={c.section} onChange={e => setClaim(i, { section: e.target.value as SectionCode })}>
              <option value="MaterialDamage">Material damage</option>
              <option value="BusinessInterruption">Business interruption</option>
              <option value="EmployersLiability">Employers' liability</option>
              <option value="PublicLiability">Public liability</option>
            </select>
            <input type="number" value={c.amount} onChange={e => setClaim(i, { amount: num(e.target.value) })} />
            <input placeholder="Description" value={c.description} onChange={e => setClaim(i, { description: e.target.value })} />
            <label className="check"><input type="checkbox" checked={c.open} onChange={e => setClaim(i, { open: e.target.checked })} /> Open</label>
            <button type="button" onClick={() => set('claims', risk.claims.filter((_, j) => j !== i))}>Remove</button>
          </div>
        ))}
        <button type="button" onClick={addClaim}>Add a claim</button>
        {err('claims')}
      </fieldset>

      <button className="primary" type="submit" disabled={busy}>{busy ? 'Submitting...' : 'Get quote'}</button>
    </form>
  );
}
