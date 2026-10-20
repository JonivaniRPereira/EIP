# Frontend EIP

Angular 22, com testes Vitest. Use Node.js **24.18.0** (`.nvmrc`) e npm **11.16.0** (`packageManager`).

## Desenvolvimento

Na pasta `frontend`:

```sh
npm ci
npm start -- --host 127.0.0.1
```

Acesse `http://127.0.0.1:4200`. A API/Gateway precisam estar disponíveis conforme o
[guia do ambiente local](../docs/guides/ambiente-local.md).

## Validação reproduzível

```sh
npm ci --no-audit --no-fund
npm run test:ci
npm run build
npm run check:fonts
```

Os testes encerram sem modo de observação e geram `test-results/frontend.xml` em
formato JUnit. O build de produção fica em `dist/frontend/browser`; a última
conferência exige fontes e licenças locais presentes, sem referências ao Google Fonts.
Para desenvolver testes em modo interativo, use `npm test`.

## Fontes e estilos

A identidade JP Code Craft usa fundo papel, verde, títulos Georgia e texto Arial
(fontes de sistema). O logotipo fica em `public/brand`. O rodapé compartilhado
fica no componente raiz e exibe ano automático, direitos reservados e crédito
de desenvolvimento. Ver [guia visual](../docs/guides/identidade-visual.md).

Roboto (pesos 300/400/500, conjunto Latin) e Material Icons vêm dos pacotes
`@fontsource/roboto` e `@fontsource/material-icons`, ambos fixados em 5.3.0 no
lockfile. Os arquivos são publicados junto com o front; não há consulta ao Google
Fonts no build nem links para esse serviço na página. A instalação inicial das
dependências continua exigindo acesso ao registro npm ou cache disponível.

As licenças OFL estão em `public/licenses` e são copiadas para o build. Ao atualizar
os pacotes, conferir e atualizar também essas cópias. `src/vendor.css` concentra
fontes/Tailwind; `src/styles.scss` mantém o tema Material sem `@import` de Sass.
Ao alterar a lista `styles` em `angular.json`, reinicie o servidor de desenvolvimento.

## CI

O job `frontend` em [ci.yml](../.github/workflows/ci.yml) executa a sequência de
validação acima, com cache npm indexado pelo lockfile. O relatório JUnit é publicado
como `frontend-test-results` por 14 dias. Os builds de imagens aguardam tanto o job
.NET quanto o frontend. Não há publicação/deploy introduzidos por esta mudança.

A validação local em 19/10/2026 teve instalação limpa, 33 testes aprovados, build e
conferência de fontes aprovados. A execução real em GitHub Actions continua pendente
até publicar as alterações e obter um resultado do runner; teste local não a substitui.
