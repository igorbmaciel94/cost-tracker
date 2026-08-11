# Provedores de cotações e câmbio para o módulo de investimentos

Pesquisa verificada em **11 de agosto de 2026**. Preços, limites e licenças mudam; devem ser reconfirmados no momento da contratação.

## Decisão recomendada

Para uma aplicação que atualiza no máximo uma vez por dia, eu separaria os dados em dois serviços:

- **Cotações de ações, ETFs e REITs:** Twelve Data como primário e Marketstack como fallback EOD.
- **Câmbio EUR/USD/BRL:** ECB Data Portal como primário e PTAX do Banco Central do Brasil como fallback.

Composição concreta para uma aplicação exibida a clientes:

1. **Twelve Data Venture (business)** para cotações. É o primeiro plano que declara acesso de exibição externa, EOD global, mais de 70 mercados e SLA de 99,95%. A página anuncia configurações a partir de USD 149/mês; o custo varia com os créditos selecionados. O plano deve ser contratado para os mercados efetivamente usados. [Preços business](https://twelvedata.com/pricing-business)
2. **Marketstack Basic** como fallback de fechamento diário. Oferece 10.000 consultas/mês, EOD, uso comercial e preço publicado de USD 9,99/mês. Antes da contratação, é necessário confirmar por escrito que a licença cobre a exibição pretendida e executar o teste de cobertura de todos os instrumentos. [Preços Marketstack](https://marketstack.com/pricing/)
3. **ECB EXR** para as taxas diárias USD/EUR e BRL/EUR, sem chave na API pública, com atribuição `Source: ECB statistics`. [Exemplos oficiais da API](https://data.ecb.europa.eu/help/api/data-examples) e [política de reutilização](https://www.ecb.europa.eu/stats/ecb_statistics/governance_and_quality_framework/html/usage_policy.en.html)
4. **BCB PTAX** para contingência de USD/BRL e EUR/BRL. O conjunto é público, OData/JSON e licenciado sob ODbL. [Catálogo oficial](https://dadosabertos.bcb.gov.br/dataset/taxas-de-cambio-todos-os-boletins-diarios)

Se o sistema continuar **estritamente pessoal, interno e não comercial**, o primário pode ser o Twelve Data Grow, anunciado a partir de USD 29/mês. O Basic gratuito não basta para a carteira descrita: ele oferece todos os EUA, mas apenas símbolos de teste internacionais; o Grow adiciona EOD global para ações e ETFs. [Preços individuais](https://twelvedata.com/pricing)

Não se deve implementar o fallback como “pegar o menor preço entre duas APIs”. Ele existe para indisponibilidade, limite excedido ou ausência de dado. Divergência entre fontes é um alerta de identidade, moeda, escala ou fechamento diferente, e não uma oportunidade de compra.

## Requisitos considerados

- Uma atualização diária é suficiente.
- A carteira inclui ações e REITs dos EUA e pode incluir instrumentos internacionais, como `VWRA` na London Stock Exchange.
- Quantidades podem ser fracionárias.
- A renda fixa tem valor informado manualmente, em BRL ou EUR.
- A moeda de apresentação e de aporte é EUR.
- Os dados alimentam uma sugestão aproximada de aporte, não execução de ordens nem marcação oficial para contabilidade.
- A aplicação pode vir a ser disponibilizada a outras pessoas; por isso, “a API funciona” e “a licença permite exibir o dado” são verificações separadas.

## Comparação de APIs de ações, ETFs e REITs

| Provedor | Cobertura e frescor úteis aqui | Autenticação | Gratuito/limites | Uso comercial e confiabilidade | Avaliação |
|---|---|---|---|---|---|
| **Twelve Data** | Suporta ações, ETFs e `REIT` como tipos explícitos; oferece `/eod`, `/quote`, `/price` e `/time_series`. EUA em tempo real no Basic; Reino Unido EOD no Grow e tempo real nos planos superiores. A própria página do provedor identifica **VWRA, LSE, XLON, moeda USD**. | API key em header `Authorization: apikey ...` ou query string. | Basic: 8 créditos/minuto e 800/dia, mas só 3 mercados e símbolos internacionais de teste. Grow: a partir de USD 29/mês, 55 créditos/minuto, sem limite diário e EOD global. Uma consulta padrão custa 1 crédito por símbolo. | Planos individuais são pessoais/internos/não comerciais. Venture declara exibição externa e SLA de 99,95%; Enterprise adiciona distribuição externa. | **Melhor primário**: cobertura confirmada do ativo difícil, modelo de tipos adequado e licença business explícita. |
| **Marketstack** | EOD global; afirma cobrir mais de 170.000 símbolos em 70 bolsas, incluindo LSE. Intraday/IEX é orientado aos EUA, o que não é necessário para este caso. | `access_key`. | Free: 100 requisições/mês, EOD e 1 ano. Basic: 10.000/mês, USD 9,99/mês e até 10 anos. Cada símbolo consultado consome uma requisição. | Basic e superiores listam “Commercial Use”. O provedor publica status e afirma uptime próximo de 100% nos últimos 365 dias, mas isso não equivale a SLA contratual. | **Bom fallback EOD de baixo custo**, sujeito a preflight de `VWRA` e confirmação de direitos de exibição. |
| **Alpha Vantage** | Mais de 100.000 símbolos globais; documentação mostra LSE com sufixo `.LON` e recomenda `SYMBOL_SEARCH` para ações, ETFs e fundos. `GLOBAL_QUOTE` é EOD por padrão; dados US em tempo real ou com 15 min de atraso exigem entitlement premium. | `apikey`. | As páginas oficiais estão inconsistentes: Premium diz limite padrão de **25/dia**, enquanto Support atualmente diz **25/minuto**. Deve-se projetar conservadoramente para 25/dia até confirmação na conta/contrato. | Termos concedem uso pessoal e não comercial por padrão; uso comercial requer acordo escrito/contato com vendas. Não encontrei SLA público. | **Fallback aceitável no MVP pessoal**, não deve ser dependência de produção antes de esclarecer limite, cobertura exata e licença. |
| **EODHD** | EOD mundial e convenção `CODE.EXCHANGE`, por exemplo `.LSE`; cobre ações, ETFs e fundos. O próprio fornecedor informa que parte do EOD/delayed vem de contratos de bolsa e parte de CFDs/market makers. | `api_token`. | Free: 20 chamadas/dia e até 1 ano de EOD. Planos pessoais EOD mundial a partir de USD 19,99/mês; pagos têm 100.000 chamadas/dia por padrão. | Planos publicados são pessoais; uso profissional, exibição ou redistribuição requer aprovação/licença comercial. Os termos dizem que os dados podem não ser em tempo real ou exatos e não garantem serviço ininterrupto. | **Alternativa secundária**, com bom volume, mas menos atraente como fonte de confiança para esta decisão. |

Fontes primárias para a tabela:

- Twelve Data: [documentação de market data](https://twelvedata.com/docs/advanced), [cobertura de mercados](https://twelvedata.com/stocks), [preços individuais](https://twelvedata.com/pricing), [preços business](https://twelvedata.com/pricing-business) e [registro oficial de VWRA](https://twelvedata.com/markets/133165/etf/lse/vwra/historical-data).
- Marketstack: [preços e limites](https://marketstack.com/pricing/), [cobertura declarada](https://marketstack.com/about), [exemplo EOD v2](https://marketstack.com/find-ticker-symbol) e [status público](https://marketstack.com/api-status).
- Alpha Vantage: [documentação](https://www.alphavantage.co/documentation/), [suporte](https://www.alphavantage.co/support/), [limite na página Premium](https://www.alphavantage.co/premium/) e [termos de serviço](https://www.alphavantage.co/terms_of_service/).
- EODHD: [quick start e limites](https://eodhd.com/financial-apis/quick-start-with-our-financial-data-apis), [preços](https://eodhd.com/pricing), [fontes de dados](https://eodhd.com/financial-apis/our-data-sources-and-data-partners), [licença comercial](https://eodhd.com/financial-apis/commercial-vs-personal-license-use) e [termos](https://eodhd.com/financial-apis/terms-conditions).

### Observação específica sobre `VWRA.L`

O ticker visto em sites ou corretoras não deve ser usado como identificador universal. No Twelve Data, o instrumento aparece como `VWRA`, exchange `LSE`, MIC `XLON` e moeda `USD`; no Alpha Vantage, a convenção documentada para LSE usa `.LON`; outros provedores podem usar `.L`, `.LSE` ou separar ticker e MIC. O Twelve Data mostra dados EOD e a moeda USD para esse instrumento. [Registro de VWRA no Twelve Data](https://twelvedata.com/markets/133165/etf/lse/vwra/historical-data)

No cadastro, persistir:

- nome e ticker exibidos ao usuário;
- MIC (`XLON`, `XNAS`, `XNYS`, etc.);
- moeda nativa da linha negociada;
- identificador estável, preferencialmente ISIN quando disponível;
- símbolo específico de cada provedor;
- escala de preço (`USD`, `GBP` ou `GBX`, por exemplo).

Isso evita tratar duas listagens do mesmo fundo como o mesmo ativo e evita o erro de 100 vezes comum em instrumentos londrinos cotados em pence. Para `VWRA`, a evidência atual é USD, mas a aplicação deve confiar no metadado retornado, não em uma regra fixa pelo sufixo.

## Comparação para câmbio EUR/USD/BRL

| Fonte | Cobertura/frequência | Autenticação e limites | Licença/confiabilidade | Uso recomendado |
|---|---|---|---|---|
| **ECB Data Portal — dataset EXR** | Taxas de referência de 30 moedas contra EUR, incluindo USD e BRL. Atualização por volta de 16:00 CET em dias úteis, exceto fechamentos TARGET. | API REST SDMX pública; os endpoints e exemplos oficiais não exigem chave. A documentação não publica uma franquia numérica, portanto “sem chave” não deve ser interpretado como “sem limite”; usar cache e requisições condicionais. | Estatísticas públicas podem ser reutilizadas gratuitamente, inclusive comercialmente, com atribuição e preservação dos dados/metadados. O ECB avisa que são taxas informativas e desencoraja uso para transações. Não há garantia de continuidade de toda série. | **Primário**, pois EUR é a moeda base do usuário e a fonte é oficial. |
| **Banco Central do Brasil — PTAX** | Boletins de câmbio para EUR desde 2002 e demais moedas desde 1984; catálogo informa atualização algumas vezes ao dia. Retorna paridades e cotações de compra/venda por data ou período. | OData/JSON público, sem chave nos endpoints documentados. Não encontrei quota numérica publicada. | Dataset sob Open Data Commons ODbL. O BCB ressalva atrasos, indisponibilidade e imprecisões. | **Fallback e verificação independente**, especialmente para ativos/valores em BRL. |

Fontes oficiais:

- ECB: [taxas de referência e horário de publicação](https://www.ecb.europa.eu/stats/policy_and_exchange_rates/euro_reference_exchange_rates/html/index.en.html), [estrutura da API](https://data.ecb.europa.eu/help/api/data), [exemplos](https://data.ecb.europa.eu/help/api/data-examples), [códigos de resposta](https://data.ecb.europa.eu/help/api/status-codes) e [política de reutilização](https://www.ecb.europa.eu/stats/ecb_statistics/governance_and_quality_framework/html/usage_policy.en.html).
- BCB: [dataset PTAX](https://dadosabertos.bcb.gov.br/dataset/taxas-de-cambio-todos-os-boletins-diarios) e [documentação OData](https://www.bcb.gov.br/conteudo/dadosabertos/BCBDepin/gnastportal-dados-abertostaxas-de-cambio---todos-os-boletins-diarios.pdf).

### Consulta ECB sugerida

A consulta abaixo recupera as duas últimas observações de USD e BRL contra EUR em CSV:

```text
GET https://data-api.ecb.europa.eu/service/data/EXR/D.USD+BRL.EUR.SP00.A?lastNObservations=2&format=csvdata
```

O ECB publica unidades da moeda por EUR. Se `r_usd` for USD por EUR e `r_brl` for BRL por EUR:

```text
valor_eur_de_usd = valor_usd / r_usd
valor_eur_de_brl = valor_brl / r_brl
brl_por_usd      = r_brl / r_usd
```

Não há vantagem em transformar toda a carteira primeiro em USD. Como aporte e apresentação são em EUR, usar **EUR como moeda-base de avaliação** reduz uma conversão, simplifica auditoria e usa diretamente a base das taxas do ECB.

## Contrato interno recomendado para os provedores

O domínio não deve conhecer JSON, nomes de endpoints nem símbolos de fornecedor. Cada adaptador deve produzir o mesmo registro normalizado:

```text
MarketQuote
  instrumentId
  provider
  providerSymbol
  mic
  price
  currency
  priceScale
  priceKind       // EOD_CLOSE | LATEST_AVAILABLE
  asOf            // instante/data efetiva do mercado
  fetchedAt       // instante da coleta
  isFallback
  rawPayloadHash

FxRate
  baseCurrency
  quoteCurrency
  rate
  rateKind        // ECB_REFERENCE | BCB_PTAX_CLOSE
  asOf
  fetchedAt
  provider
  isFallback
```

Regras importantes:

- Usar `decimal`, nunca ponto flutuante binário, para dinheiro, preço, quantidade e taxa.
- Calcular `valor_nativo = quantidade_fracionaria × preço` antes de converter para EUR.
- Usar o fechamento **não ajustado** para valor atual. `adjusted close` serve para séries de retorno; splits também exigem ajustar a quantidade/custo histórico, não apenas trocar o preço.
- Nunca substituir cotação ausente por zero. Manter o último valor bom e marcar como desatualizado, ou bloquear a sugestão de aporte quando ultrapassar o limite de frescor.
- Persistir o payload bruto ou ao menos seu hash e os metadados para explicar posteriormente qual cotação gerou uma recomendação.
- Fazer o mesmo snapshot de preços e FX alimentar cálculo, tela e histórico; não consultar uma API separadamente em cada request do usuário.

## Fluxo diário e fallback

Uma coleta às **06:00 Europe/Lisbon** é mais previsível: normalmente já existe fechamento final do dia anterior tanto para LSE quanto para os EUA e a taxa ECB do dia anterior. A aplicação ainda pode oferecer atualização manual, mas com a mesma semântica de “último valor disponível”.

Fluxo sugerido:

1. Resolver e validar o catálogo de instrumentos no cadastro; não fazer descoberta por texto em toda atualização.
2. Buscar os últimos fechamentos no Twelve Data em lote quando possível.
3. Validar presença de preço, moeda, MIC e `asOf`; rejeitar resposta parcial ou instrumento diferente.
4. Em timeout, `429`, erro 5xx ou ausência de observação após tentativas curtas, consultar Marketstack para aquele instrumento.
5. Buscar `USD+BRL` no ECB em uma única chamada.
6. Se o ECB estiver indisponível ou sem observação nova esperada, consultar PTAX para USD e EUR.
7. Gravar snapshot imutável, origem e timestamp; recalcular a carteira e as sugestões a partir dele.
8. Se os dois provedores falharem, conservar o último snapshot e exibir o atraso. Não recalcular uma recomendação como se o valor fosse atual.

Uma diferença grande entre primário e fallback deve gerar diagnóstico, não média automática. Como ponto inicial operacional, investigar divergência acima de 3% ou qualquer diferença de moeda/MIC/data; a causa costuma ser fechamento de sessões distintas, escala GBX/GBP, corporate action ou ativo homônimo.

## Por que a interface não deve prometer “tempo real”

Mesmo quando um plano de provedor chama um endpoint de “real-time”, este produto não é tempo real:

- o job consulta uma vez ao dia;
- o preço pode ser EOD ou o último disponível, dependendo do mercado e do plano;
- EUA e LSE fecham em horários e feriados diferentes;
- o fallback pode devolver o fechamento anterior;
- o `GLOBAL_QUOTE` do Alpha Vantage é explicitamente EOD por padrão, e os dados US em tempo real/15 minutos são entitlements premium regulados; [documentação oficial](https://www.alphavantage.co/documentation/)
- o ECB atualiza apenas em dias úteis, por volta das 16:00 CET, e classifica a taxa como referência informativa, não taxa executável; [taxas de referência](https://www.ecb.europa.eu/stats/policy_and_exchange_rates/euro_reference_exchange_rates/html/index.en.html)
- preço de tela não inclui necessariamente spread, comissão, imposto nem conversão efetivamente oferecida pela corretora.

Textos de UI adequados:

- `Último fechamento disponível`
- `Cotação estimada em EUR`
- `Preço de 10/08/2026 · fonte Twelve Data`
- `Câmbio de referência ECB de 10/08/2026`
- `Dados desatualizados — última coleta há 2 sessões`

Evitar: `preço agora`, `tempo real`, `valor garantido` ou uma sugestão de aporte sem `asOf` visível.

## Critérios de aceite antes de integrar

1. Executar um **preflight de símbolos** com a carteira real, no mínimo `KO`, `O` e `VWRA`/`XLON`; salvar identificador, MIC e moeda retornados.
2. Confirmar por escrito a licença aplicável: uso pessoal, exibição externa para clientes e eventual redistribuição são categorias diferentes.
3. Validar em 10 pregões o horário em que cada provedor consolida o EOD e comparar com a página oficial da bolsa/corretora.
4. Testar fim de semana, feriado apenas nos EUA, feriado apenas na LSE e fechamento TARGET.
5. Testar símbolo inexistente, ticker homônimo em bolsas diferentes, `429`, timeout, resposta parcial e chave revogada.
6. Testar USD, EUR, BRL, GBP e GBX; rejeitar moeda ou escala ausente.
7. Definir a política de frescor em **sessões de mercado**, não apenas horas corridas. Sugestão inicial: aviso após uma sessão esperada sem atualização e bloqueio da recomendação após duas sessões, salvo override explícito do usuário.
8. Monitorar taxa de sucesso, idade do dado, uso do fallback, divergência de preço e consumo de quota.

## Conclusão

Para o problema descrito, dados EOD são suficientes e mais coerentes do que pagar por streaming. Twelve Data tem a evidência de cobertura mais forte para a combinação EUA + REITs + ETF londrino `VWRA`; Marketstack oferece um fallback EOD comercial barato. ECB e BCB são fontes oficiais e complementares para a conversão diária. A arquitetura deve tratar cada valor como um **snapshot aproximado, datado e auditável**, e nunca como uma cotação executável em tempo real.
