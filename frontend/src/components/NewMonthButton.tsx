interface NewMonthButtonProps {
  disabled?: boolean;
  loading?: boolean;
  onClick: () => void;
}

export function NewMonthButton({ disabled, loading, onClick }: NewMonthButtonProps) {
  return (
    <button
      type="button"
      className="cta-new-month"
      onClick={onClick}
      disabled={disabled || loading}
    >
      {loading ? 'Criando...' : 'Novo mês'}
    </button>
  );
}
