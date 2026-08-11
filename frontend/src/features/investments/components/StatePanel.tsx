import type { ReactNode } from 'react';

interface StatePanelProps {
  title: string;
  children?: ReactNode;
  action?: ReactNode;
  tone?: 'neutral' | 'warning' | 'danger';
}

export function StatePanel({ title, children, action, tone = 'neutral' }: StatePanelProps) {
  return (
    <section className="investment-state" data-tone={tone} role={tone === 'danger' ? 'alert' : 'status'}>
      <div>
        <h2>{title}</h2>
        {children && <div className="investment-state-copy">{children}</div>}
      </div>
      {action}
    </section>
  );
}
