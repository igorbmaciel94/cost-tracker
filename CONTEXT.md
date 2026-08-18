# Cost Tracker

O produto reúne planejamento financeiro pessoal e acompanhamento de uma carteira de investimentos. O contexto de investimentos existe para avaliar posições em uma moeda comum e orientar novos aportes sem executar ordens financeiras.

## Investimentos

**Carteira**:
O conjunto contínuo de posições de investimento acompanhado pelo usuário.
_Evitar_: Mês de investimento, conta

**Classe de ativo**:
Uma das divisões estratégicas da carteira: Stocks, REITs, Renda Fixa Brasil ou Renda Fixa Internacional.
_Evitar_: Tipo, setor

**Instrumento**:
Um ativo investível específico, identificado pelo mercado onde é negociado ou por um nome próprio quando não possui ticker.
_Evitar_: Ticker, papel

**Posição**:
A exposição atualmente mantida em um instrumento, expressa por quantidade ou por uma avaliação manual.
_Evitar_: Ativo, aporte

**Meta de alocação**:
A participação desejada de uma classe de ativo na carteira; o conjunto das metas totaliza 100%.
_Evitar_: Distribuição atual, limite

**Nota de alocação**:
Uma avaliação positiva definida pelo usuário que determina o peso relativo desejado de um instrumento dentro da sua classe. Ela não representa preço justo nem recomendação de compra.
_Evitar_: Nota de preço, valuation

**Moeda base**:
A moeda na qual o patrimônio e os cálculos consolidados são apresentados ao usuário.
_Evitar_: Moeda do instrumento, moeda da cotação

**Cotação**:
O preço observado de um instrumento em sua moeda de negociação, associado a uma fonte e a uma data de referência.
_Evitar_: Valor justo, preço em tempo real

**Avaliação manual**:
O saldo corrente informado pelo usuário para uma posição sem cotação automática.
_Evitar_: Aporte, custo de aquisição

**Plano de aporte**:
Uma simulação imutável de como distribuir um valor disponível, baseada nas posições, metas, cotações e câmbio de uma data de referência.
_Evitar_: Ordem, compra, aporte executado

**Movimentação**:
Um aporte, compra, venda, retirada ou ajuste que o usuário confirma como efetivamente realizado.
_Evitar_: Sugestão, plano de aporte

**Evento de dividendo**:
Um dividendo divulgado e cadastrado para um instrumento, definido pelo valor por unidade, moeda, data ex e data de pagamento.
_Evitar_: Dividendo previsto, rendimento estimado

**Data ex**:
A data de mercado que determina o direito ao dividendo; a quantidade elegível é a posição mantida antes dessa data.
_Evitar_: Data de pagamento, data do crédito

**Crédito de dividendo**:
O valor imutável reconhecido na data de pagamento a partir de um evento de dividendo e da quantidade elegível. Ele não altera a quantidade nem o valor da posição que o originou.
_Evitar_: Aporte, valorização, compra

**Caixa de dividendos**:
O saldo não investido, separado por moeda, formado por créditos de dividendos e disponível para um aporte futuro.
_Evitar_: Posição, saldo do ativo, patrimônio investido

**Desvio de alocação**:
A diferença entre a participação atual e a meta de uma classe ou instrumento.
_Evitar_: Lucro, desconto
