# 12 - Dashboard Builder

**Projeto:** Enterprise Intelligence Platform (EIP)  
**Versão:** 1.0  
**Status:** Oficial  
**Última atualização:** Julho/2026

---

# 1. Objetivo

O Dashboard Builder permite criar, publicar e compartilhar painéis analíticos sem expor SQL, tabelas de ERP ou detalhes do Data Warehouse. Cada visualização utiliza consultas declarativas do Analytics Engine e métricas certificadas da Camada Semântica.

O objetivo é entregar autonomia controlada: usuários montam análises com componentes e campos autorizados, enquanto a plataforma mantém consistência de cálculo, isolamento de tenant e desempenho previsível.

```text
Dashboard → Visualização → Consulta declarativa → Analytics Engine → Semântica → DW
```

---

# 2. Princípios

- **Semântica antes de visualização:** gráficos usam datasets, métricas e dimensões publicados.
- **Configuração declarativa:** layout e consulta são documentos versionados, não código executável.
- **Segurança herdada:** acesso ao dashboard nunca amplia acesso aos dados subjacentes.
- **Publicação controlada:** rascunho, revisão, publicação e arquivamento são estados explícitos.
- **Reutilização:** filtros, temas, métricas e componentes são reutilizáveis dentro do escopo autorizado.
- **Desempenho previsível:** limites de widgets, consultas e cardinalidade protegem a experiência.
- **Explicabilidade:** toda visualização exibe métrica, período, filtros e frescor quando solicitado.
- **Acessibilidade:** contraste, navegação por teclado, rótulos e alternativas textuais são requisitos de UI.

---

# 3. Modelo de Domínio

| Entidade | Responsabilidade |
|---|---|
| Dashboard | painel com título, dono, workspace, estado, layout e permissões |
| DashboardVersion | versão imutável de rascunho ou publicação |
| Page | página/seção de um dashboard |
| Widget | bloco visual: KPI, gráfico, tabela, texto, filtro ou alerta |
| Visualization | configuração de tipo de gráfico, eixos, séries, formatação e interação |
| QueryDefinition | consulta declarativa validada pelo Analytics Engine |
| Filter | filtro de página, dashboard ou widget |
| Theme | tokens visuais reutilizáveis e acessíveis |
| SharePolicy | regras de visualização, edição, publicação e exportação |

Todos os recursos pertencem a um tenant. Dashboard, versão, compartilhamento e filtros também pertencem a um workspace.

---

# 4. Ciclo de Vida

```text
Draft → InReview → Published → Archived
  ↑       │             │
  └───────┴─────────────┘
```

- **Draft:** editável por autores autorizados; não é a referência para usuários finais.
- **InReview:** versão preparada para validação técnica/negócio quando o processo exigir.
- **Published:** versão imutável e visível conforme SharePolicy.
- **Archived:** preservada para histórico, sem novas edições ou uso normal.

Editar um publicado cria novo rascunho. A publicação troca a versão ativa de forma atômica e registra autor, data, alterações e aprovação quando aplicável.

---

# 5. Tipos de Widget Iniciais

| Tipo | Uso | Limites iniciais |
|---|---|---|
| KPI | valor atual, comparação e tendência resumida | uma ou poucas métricas por widget |
| Line/Area chart | evolução temporal | tempo como dimensão principal |
| Bar/Column chart | comparação por categoria | cardinalidade limitada e ordenação obrigatória |
| Donut/Pie chart | composição simples | poucas categorias; não usar para séries extensas |
| Table | detalhe tabular autorizado | paginação/limite de linhas |
| Text/Markdown seguro | contexto, definição e instruções | sem HTML/script executável |
| Filter control | período, empresa, categoria e outros filtros permitidos | campos publicados pelo dataset |

Mapas, tabelas dinâmicas, gráficos avançados, drill-through complexo e visuais de terceiros entram somente após requisitos, segurança e desempenho serem definidos.

---

# 6. Especificação Declarativa

Um widget armazena configuração validada e não executa código do usuário.

```json
{
  "id": "a10d0a2c-6e34-41d1-b01f-9c1d3d2b9fd7",
  "type": "lineChart",
  "title": "Receita líquida mensal",
  "query": {
    "dataset": "sales",
    "metrics": ["netRevenue"],
    "dimensions": ["date.month"],
    "filters": [],
    "orderBy": [{ "field": "date.month", "direction": "asc" }]
  },
  "visualization": {
    "xAxis": "date.month",
    "yAxis": "netRevenue",
    "format": "currency",
    "legend": false
  },
  "layout": { "x": 0, "y": 0, "w": 6, "h": 4 }
}
```

O backend valida dataset, métricas, dimensões, filtros, tipo de gráfico, layout e permissões antes de salvar ou publicar. Propriedades desconhecidas são rejeitadas ou ignoradas conforme versão de schema documentada.

---

# 7. Filtros e Interação

## 7.1 Hierarquia

Filtros são aplicados na seguinte ordem:

```text
Políticas de segurança → Contexto de tenant/workspace/empresa → Filtros de dashboard → Filtros de página → Filtros de widget → Interação temporária do usuário
```

Um filtro de usuário somente reduz o escopo autorizado. Ele não remove políticas de segurança ou inclui empresa/workspace fora da permissão.

## 7.2 Tipos

- período e comparação temporal;
- empresa, filial ou escopo organizacional autorizado;
- dimensão categórica com valores publicados;
- intervalo numérico/data;
- filtro de contexto cruzado entre widgets compatíveis.

O Dashboard Builder não aceita expressões SQL, scripts, filtros por campos físicos ou lista ilimitada de valores.

## 7.3 Drill-down e detalhe

Drill-down é permitido somente em hierarquias declaradas no dataset. Drill-through abre detalhe por rota/consulta aprovada, aplicando as mesmas permissões, limites e auditoria de exportação quando houver dados sensíveis.

---

# 8. Segurança e Compartilhamento

## 8.1 Acesso efetivo

A SharePolicy pode conceder descoberta, visualização, edição, publicação ou administração do dashboard dentro do tenant/workspace. Ao renderizar um widget, o Analytics Engine calcula o acesso efetivo aos dados.

Consequentemente, dois usuários podem ver o mesmo dashboard com resultados diferentes ou widget indisponível, quando seus escopos de empresas/dados diferirem. A interface deve comunicar isso sem revelar informações não autorizadas.

## 8.2 Compartilhamento externo e embeds

Links públicos e embeds não fazem parte do MVP. Qualquer futura exposição externa exige identidade, token de curta duração, escopo de dados fixo, expiração, rate limit, revogação e auditoria.

## 8.3 Exportação

Exportar PDF, imagem, CSV ou XLSX exige permissão específica e obedece à classificação de dados. Exportações são jobs assíncronos, auditados, com expiração de download e sem possibilidade de elevar o nível de detalhe além do que o usuário vê.

---

# 9. Desempenho e Cache

## 9.1 Orçamento de renderização

O MVP define limites configuráveis por plano, incluindo número máximo de páginas, widgets por página, consultas concorrentes, pontos por série, categorias e tempo de renderização.

Widgets são agrupados por consulta equivalente para reduzir chamadas repetidas. Consultas independentes são executadas com concorrência limitada e canceladas quando o usuário abandona a página.

## 9.2 Cache

O resultado de um widget pode reutilizar cache do Analytics Engine. A chave inclui tenant, workspace, escopo de permissão, versão semântica, versão do dashboard, filtros normalizados e versão/frescor dos dados.

Configuração de dashboard pode ser cacheada separadamente do resultado. Alterar permissões, publicação, métrica ou dado relevante invalida o item correspondente.

## 9.3 Estados de erro

Cada widget pode exibir carregamento, vazio, aviso de frescor, acesso insuficiente ou erro recuperável. A falha de um widget não deve impedir a renderização dos demais.

---

# 10. UX, Acessibilidade e Governança Visual

- gráficos têm título, descrição e alternativa textual de valores principais;
- cores não são o único meio de transmitir informação;
- contraste, foco, navegação por teclado e leitores de tela seguem padrão de acessibilidade adotado;
- valores monetários, datas, número e idioma respeitam preferências do tenant/usuário;
- dashboards exibem última atualização e filtros ativos;
- temas são versionados e não permitem CSS/JavaScript arbitrário;
- templates certificados podem acelerar a criação de painéis de vendas, financeiro e estoque.

---

# 11. IA Assistida

A IA pode propor um dashboard ou alteração como rascunho estruturado. O fluxo obrigatório é:

1. usuário descreve a necessidade;
2. AI Engine consulta catálogo e permissões;
3. IA propõe widgets, consultas e layout válidos;
4. backend valida a especificação pelo mesmo schema do Builder;
5. usuário revisa, edita e confirma publicação.

A IA não publica, compartilha ou exporta dashboards sem ferramenta autorizada e confirmação exigida pela política.

---

# 12. APIs Iniciais

| Endpoint | Finalidade |
|---|---|
| `GET /api/v1/dashboards` | listar dashboards visíveis no contexto atual |
| `POST /api/v1/dashboards` | criar rascunho |
| `GET /api/v1/dashboards/{id}` | obter metadados e versão permitida |
| `PATCH /api/v1/dashboards/{id}` | editar rascunho com controle de concorrência |
| `POST /api/v1/dashboards/{id}/publish` | publicar versão validada |
| `POST /api/v1/dashboards/{id}/archive` | arquivar dashboard |
| `POST /api/v1/dashboards/{id}/query` | obter dados de widgets no contexto autorizado |
| `POST /api/v1/dashboards/{id}/exports` | solicitar exportação assíncrona |

Contratos usam versionamento, ETag, Problem Details, permissões e jobs conforme o documento de API Design.

---

# 13. Critérios de Publicação

Um dashboard só pode ser publicado quando:

- todas as consultas usam datasets/métricas/dimensões autorizados;
- filtros e interações foram validados;
- não há expressão, script ou fonte externa não permitida;
- layout, títulos e formatação mínima estão válidos;
- owner, workspace e SharePolicy foram definidos;
- desempenho foi avaliado dentro do orçamento;
- frescor e qualidade dos dados são compatíveis com a finalidade;
- permissões e visualização por escopo foram testadas;
- alterações e publicação foram auditadas.

---

# 14. Fora do Escopo Inicial

Não fazem parte do MVP:

- editor de SQL, JavaScript, HTML ou visuais de terceiros;
- compartilhamento público anônimo;
- pixel-perfect reporting e paginação de impressão complexa;
- colaboração em tempo real em um mesmo rascunho;
- marketplace de templates/visuais;
- substituição de toda a funcionalidade avançada de ferramentas de BI desde a primeira versão.

O foco inicial é criar painéis rápidos, consistentes, seguros e baseados nas métricas certificadas da EIP.
