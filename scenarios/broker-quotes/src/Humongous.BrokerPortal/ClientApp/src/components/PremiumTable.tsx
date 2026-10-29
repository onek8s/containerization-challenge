import type { Premium } from '../api/types';
import { formatMoney, formatPercent, sectionNames } from '../format';

export function PremiumTable({ premium }: { premium: Premium }) {
  return (
    <table className="premium">
      <tbody>
        {premium.sections.map(s => (
          <tr key={s.section}>
            <td>
              {sectionNames[s.section]}
              {s.minimumApplied > 0 && <span className="note"> (minimum premium)</span>}
              {s.loadingPercent !== 0 && <span className="note"> ({s.loadingPercent > 0 ? '+' : ''}{s.loadingPercent}% underwriter)</span>}
            </td>
            <td className="num">{formatMoney(s.gross)}</td>
          </tr>
        ))}
        <tr className="subtotal"><td>Gross premium</td><td className="num">{formatMoney(premium.grossPremium)}</td></tr>
        <tr><td>Your commission ({formatPercent(premium.commissionRate)})</td><td className="num">{formatMoney(premium.commission)}</td></tr>
        <tr><td>Insurance premium tax ({formatPercent(premium.iptRate)})</td><td className="num">{formatMoney(premium.ipt)}</td></tr>
        {premium.fee > 0 && <tr><td>Policy fee</td><td className="num">{formatMoney(premium.fee)}</td></tr>}
        <tr className="total"><td>Total payable by client</td><td className="num">{formatMoney(premium.total)}</td></tr>
      </tbody>
    </table>
  );
}
