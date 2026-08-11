import { Navigate, Route, Routes } from 'react-router-dom';
import { InvestmentsShell } from './components/InvestmentsShell';
import { AllocationPage } from './pages/AllocationPage';
import { ContributionPage } from './pages/ContributionPage';
import { InstrumentDetailPage } from './pages/InstrumentDetailPage';
import { InstrumentFormPage } from './pages/InstrumentFormPage';
import { PortfolioPage } from './pages/PortfolioPage';
import './investments.css';

export function InvestmentsPage() {
  return (
    <InvestmentsShell>
      <Routes>
        <Route index element={<PortfolioPage />} />
        <Route path="alocacao" element={<AllocationPage />} />
        <Route path="aporte" element={<ContributionPage />} />
        <Route path="ativos/novo" element={<InstrumentFormPage />} />
        <Route path="ativos/:instrumentId/editar" element={<InstrumentFormPage />} />
        <Route path="ativos/:instrumentId" element={<InstrumentDetailPage />} />
        <Route path="*" element={<Navigate to="/investimentos" replace />} />
      </Routes>
    </InvestmentsShell>
  );
}
