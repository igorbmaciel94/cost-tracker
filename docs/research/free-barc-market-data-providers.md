# Provedores gratuitos de EOD para Barclays na LSE

Pesquisa verificada em **19 de agosto de 2026**, usando fontes oficiais dos provedores e da London Stock Exchange. Preços, franquias e cobertura podem mudar; o preflight com uma chave própria continua sendo obrigatório.

## Conclusão

A alternativa gratuita mais plausível ao Yahoo Finance para `BARC` é o **Alpha Vantage**. A documentação oficial inclui ações da London Stock Exchange no endpoint diário global, usa o sufixo `.LON` e permite as 100 observações mais recentes com chave gratuita. A franquia gratuita atual é de **25 chamadas por dia**, suficiente para uma consulta EOD diária de `BARC`. O fornecedor também declara que licencia dados oficialmente da LSE. [Documentação do `TIME_SERIES_DAILY`](https://www.alphavantage.co/documentation/#daily), [limite gratuito](https://www.alphavantage.co/support/) e [origens/cobertura](https://www.alphavantage.co/stock_api_landing/)

O símbolo esperado é `BARC.LON`: a LSE confirma o ticker `BARC`, MIC `XLON`, moeda de cotação `GBX` e ISIN `GB0031348658`; o Alpha Vantage documenta `.LON` como sua convenção para a LSE. Esse identificador ainda deve ser confirmado pelo `SYMBOL_SEARCH` com uma chave gratuita real, porque a chave pública `demo` atualmente não aceita consultas arbitrárias. [Instrumento oficial na LSE](https://www.londonstockexchange.com/stock/BARC/barclays-plc/company-page) e [busca de símbolos do Alpha Vantage](https://www.alphavantage.co/documentation/#symbolsearch)

Não foi encontrado outro provedor que possa ser afirmado, sem credenciais, como fonte gratuita e atual de `BARC/XLON`. O **London Strategic Edge** é uma segunda tentativa experimental: declara API REST gratuita, sem cartão, candles diários e ações internacionais, mas não expõe publicamente a lista que permitiria confirmar BARC nem a linhagem do feed. [API gratuita](https://londonstrategicedge.com/free-market-data-api/) e [limites do databank](https://www.londonstrategicedge.com/data/)

Recomendação operacional:

1. Criar primeiro uma chave gratuita do Alpha Vantage e executar o preflight abaixo.
2. Se `BARC.LON` devolver o último pregão com preço compatível com a LSE, usar Alpha Vantage para `BARC`, mantendo o Yahoo apenas como último fallback temporário.
3. Se o Alpha Vantage não confirmar o instrumento ou devolver dado atrasado, testar a busca de BARC no London Strategic Edge antes de desenvolver outro adaptador.
4. Manter Marketstack somente como fallback para ativos dos EUA que passem no preflight, não para `BARC`.

## Comparação

| Provedor | Plano grátis | Cobertura relevante | Símbolo provável | Veredito para `BARC` |
|---|---:|---|---|---|
| **Alpha Vantage** | 25 chamadas/dia | `TIME_SERIES_DAILY` global; LSE documentada; 100 pontos no `compact` disponível para chaves grátis | `BARC.LON` | **Melhor candidato**, pendente de um único preflight com chave própria |
| **London Strategic Edge** | API grátis; 10 downloads/hora no databank | Declara 3.987 ações US e internacionais e candles de `1d` | não confirmado | Candidato experimental; BARC e origem do feed precisam de preflight |
| **EODHD** | 20 chamadas/dia | As páginas oficiais divergem: a documentação EOD menciona qualquer ticker, mas a oferta grátis geral limita preços/fundamentos aos EUA | `BARC.LSE` no plano global | Não considerar BARC grátis sem confirmação escrita do fornecedor |
| **Marketstack** | 100 chamadas/mês; EOD; 1 ano | Declara EOD global, mas o teste do projeto para `BARC.XLON` devolveu como última observação `2023-10-18` | `BARC.XLON` | **Não usar para BARC**; o plano pago Basic não promete corrigir esse feed |
| **Twelve Data** | 8 créditos/minuto e 800/dia | Grátis para ações/ETFs dos EUA; internacional apenas em símbolos de teste | `BARC`, exchange `LSE`, MIC `XLON` | Catálogo e preço atuais existem, mas o EOD global requer Grow ou superior |
| **FMP** | 250 chamadas/dia | O Basic grátis oferece EOD apenas para um conjunto limitado; cobertura UK aparece no Premium | normalmente ticker com extensão de bolsa | Não é uma alternativa gratuita para BARC |
| **Finnhub** | 60 chamadas/minuto | O plano grátis de market data é US; OHLC internacional/LSE aparece nos planos pagos | — | Não atende gratuitamente |
| **Tiingo** | 1.000 chamadas/dia e 500 símbolos/mês | O EOD grátis é centrado em ações dos EUA e China | — | Não cobre a linha principal de Londres |

### Alpha Vantage

O endpoint recomendado é `TIME_SERIES_DAILY`, não `GLOBAL_QUOTE`, porque a aplicação precisa de um fechamento datado e verificável. A documentação declara que:

- o endpoint devolve OHLCV diário global;
- `outputsize=compact`, disponível para chaves gratuitas, traz as 100 observações mais recentes;
- o exemplo oficial de LSE usa `TSCO.LON`;
- a busca de símbolos cobre ações, ETFs e fundos globais.

Fontes: [documentação diária](https://www.alphavantage.co/documentation/#daily), [busca de símbolos](https://www.alphavantage.co/documentation/#symbolsearch) e [suporte/limites](https://www.alphavantage.co/support/).

Há uma restrição importante de licença: os termos concedem uso pessoal e não comercial por padrão. Uso por empresa, exibição para terceiros ou outra atividade comercial exige acordo por escrito com o Alpha Vantage. Para o Cost Tracker pessoal isso pode ser aceitável; para disponibilizar o produto a clientes, é necessário tratar a licença separadamente. [Termos oficiais](https://www.alphavantage.co/terms_of_service/)

Preflight sugerido, depois de criar a chave:

```text
GET https://www.alphavantage.co/query?function=SYMBOL_SEARCH&keywords=Barclays&apikey=<KEY>
GET https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol=BARC.LON&outputsize=compact&apikey=<KEY>
```

Critérios para aprovar:

- a busca deve identificar Barclays PLC, Reino Unido/London Stock Exchange;
- a série deve conter o último pregão esperado, não apenas um registro antigo;
- o preço deve ser compatível com a cotação oficial em `GBX`;
- o mapping deve preservar `BARC`, MIC `XLON`, moeda nativa `GBP` e multiplicador `0,01`, pois 500 GBX equivalem a GBP 5,00.

### London Strategic Edge

O London Strategic Edge declara uma API REST gratuita para mais de 16.000 instrumentos, com 3.987 ações americanas e internacionais, candles de um minuto a um dia, JSON/CSV e até 5.000 linhas por requisição. O databank permite 10 downloads por hora, de até um milhão de linhas cada. [Descrição da API](https://londonstrategicedge.com/free-market-data-api/) e [databank/limites](https://www.londonstrategicedge.com/data/)

Entretanto, as páginas públicas não permitem confirmar se `BARC` está entre as ações nem revelam a fonte do feed. Portanto, o serviço só deve avançar se uma chave gratuita comprovar o instrumento, a data atual, a moeda/unidade e a independência em relação ao Yahoo.

### EODHD

O EODHD usa a convenção `CODE.EXCHANGE`; sua documentação mostra que a London Exchange tem código `LSE`, MIC `XLON` e moeda padrão `GBP`. O endpoint diário para o candidato seria:

```text
GET https://eodhd.com/api/eod/BARC.LSE?api_token=<KEY>&fmt=json&from=<YYYY-MM-DD>
```

A documentação EOD diz que o plano gratuito dá acesso a qualquer ticker no último ano, com 20 chamadas/dia, mas a página geral da oferta descreve o plano gratuito como limitado a EOD/fundamentos dos EUA. Como as próprias fontes oficiais divergem e a chave `demo` não aceita BARC, não há evidência suficiente para indicar EODHD como alternativa gratuita para a LSE. O símbolo `BARC.LSE` continua válido para um eventual preflight ou plano global pago. [API EOD](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes), [preços](https://eodhd.com/pricing) e [lista de bolsas](https://eodhd.com/financial-apis/exchanges-api-list-of-tickers-and-trading-hours)

### Marketstack e Twelve Data

O plano Free do Marketstack publica 100 chamadas mensais, EOD e um ano de histórico. O Basic pago aumenta volume e histórico e adiciona uso comercial, mas não declara uma cobertura diferente para `BARC`; portanto não há base para pagar esperando que `BARC.XLON` deixe de retornar 2023. [Preços oficiais](https://marketstack.com/pricing)

Para ativos americanos, o Marketstack ainda pode ser útil como fallback após validar que cada símbolo retorna o último pregão. Como 100 chamadas/mês são poucas para consultar toda a carteira diariamente, ele deve ser acionado somente após falha, limite ou dado bloqueado do Twelve Data.

O Twelve Data identifica oficialmente Barclays como `BARC`, LSE, MIC `XLON`, cotado em `GBp`, e mantém uma página atual do instrumento. Porém, o Basic gratuito lista ações americanas e apenas símbolos internacionais de teste; EOD global aparece no Grow. Logo, a indisponibilidade gratuita de BARC é uma regra do plano, não ausência do instrumento no catálogo. [BARC no Twelve Data](https://twelvedata.com/markets/139622/stock/lse/barc) e [preços/cobertura](https://twelvedata.com/pricing)

## Provedores descartados para este caso

- **Financial Modeling Prep:** o plano Basic grátis oferece EOD e 250 chamadas/dia, mas a própria matriz marca símbolos limitados; cobertura UK aparece no Premium, atualmente a partir de US$ 59/mês no anual. [Preços FMP](https://site.financialmodelingprep.com/developer/docs/pricing)
- **Finnhub:** a tabela oficial coloca o plano gratuito em cobertura US; OHLC de LSE aparece apenas nos pacotes pagos de market data. [Preços de market data](https://www.finnhub.io/pricing-stock-api-market-data) e [preços da API](https://api.finnhub.io/pricing)
- **Tiingo:** oferece um plano gratuito generoso, mas sua cobertura de ações EOD é centrada em EUA e ações chinesas; não é uma fonte da linha `BARC` em Londres. [Preços Tiingo](https://app.tiingo.com/pricing/) e [descrição oficial de cobertura](https://www.tiingo.com/blog/best-stock-price-api)
- **Google Finance:** é uma função de planilha, não uma API REST adequada ao backend; a própria ajuda limita acesso histórico pela API do Sheets/Apps Script e alerta que a função não suporta a maioria das bolsas internacionais. [Ajuda oficial](https://support.google.com/docs/answer/3093281)
- **Stooq:** há downloads CSV públicos, mas não foi encontrada documentação oficial de API, franquia, estabilidade ou licença adequada para automação. Não é prudente substituir um endpoint não contratado do Yahoo por outro endpoint não documentado.

## Decisão antes de implementar

Não alterar ainda a ordem dos provedores com base apenas na cobertura declarada. A próxima ação segura é criar uma chave Alpha Vantage, executar duas chamadas e guardar o payload do preflight. Se a data de `BARC.LON` for atual e o preço em GBX bater aproximadamente com a [página oficial da LSE](https://www.londonstockexchange.com/stock/BARC/barclays-plc/company-page), há evidência suficiente para projetar o adaptador e o mapping específico do provedor.
