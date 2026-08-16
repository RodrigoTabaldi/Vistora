# Vistora

**Plataforma SaaS de vistoria imobiliária** — laudos de entrada, saída e manutenção com evidências fotográficas georreferenciadas, checklists personalizáveis e trilha de auditoria.

Feita para quem faz vistoria de verdade: o vistoriador preenche pelo celular em campo, a imobiliária acompanha pelo computador no escritório.

---

## O problema que resolve

A vistoria imobiliária é o documento que sustenta a devolução de um imóvel — e, na prática, ainda é feita em papel, planilha ou WhatsApp. Quando chega a hora de cobrar um dano na saída, faltam as provas: não se sabe a data da foto, nem o local, nem o estado na entrada.

O Vistora resolve isso ligando três coisas que normalmente ficam soltas:

| Dor | Como o Vistora trata |
|---|---|
| Checklist genérico que não serve para o imóvel | 14 modelos profissionais por tipo de imóvel + criação de modelos próprios |
| Foto sem valor probatório | Cada evidência grava data/hora, geolocalização, autor e **hash SHA-256** |
| Leitura de medidores e chaves esquecida | Bloco **Medidores e chaves** padrão em todo modelo (hidrômetro, luz, gás, chaves, controles) |
| Não saber o andamento da equipe | Painel com conclusão por vistoria, pendências e linha do tempo de auditoria |

---

## Funcionalidades

### Vistoria em campo
- Checklist **agrupado por ambiente**, com contador de progresso (`8/14 avaliados`)
- Três estados por tópico — **Bom · Regular · Irregular** — com alvos de toque grandes
- **Salvamento automático**: cada marcação e observação vai para a API na hora
- Adicionar **ambientes e tópicos personalizados** durante a vistoria, sem sair da tela
- Foto pela câmera do celular com **geolocalização + hash de integridade**
- Conclusão recalcula percentual preenchido e número de pendências

### Modelos de vistoria
14 modelos prontos, cobrindo **1.220 tópicos** catalogados:

| Residencial | Comercial | Outros |
|---|---|---|
| Studio / Kitnet | Loja / ponto comercial | Terreno / lote |
| Apartamento 1 quarto | Sala comercial / escritório | Imóvel mobiliado (inventário) |
| Apartamento 2Q/1B e 2Q/2B | Galpão / depósito | Manutenção periódica |
| Apartamento 3Q com suíte | | |
| Casa 2Q, Casa 3Q com suíte, Casa com piscina | | |

Todo modelo inclui dois blocos que a prática exige:

- **Medidores e chaves** — leitura de hidrômetro, relógio de luz e gás; chaves principais e de serviço; controle de portão; tag de acesso
- **Instalações gerais** — quadro de disjuntores, fiação, caixa d'água, registros, infiltrações, fissuras estruturais, interfone

Os ambientes são completos: banheiro tem 14 tópicos (box, vedação, ralo, sifão, acessórios), cozinha tem 13 (ponto de gás, exaustor, sifão). Cada cômodo cobre piso, paredes, teto, rodapés, portas, janelas, tomadas, interruptores e luminárias.

Além disso, a imobiliária **cria seus próprios modelos** pelo construtor visual — adiciona ambientes, tópicos e salva como padrão da casa.

### Ciclo completo da locação
- **Contratos e partes** — locador, locatário, fiador e procurador; documento exibido mascarado (LGPD)
- **Tipos de vistoria** — entrada, saída, periódica, manutenção, recebimento de chaves, pré-compra/venda, captação, temporada, sinistro e inspeção predial
- **Check-in/check-out em campo** com data, hora e GPS
- **Medidores, chaves e inventário** de imóvel mobiliado registrados por vistoria
- **Item enriquecido** — severidade, classificação (desgaste natural, dano do locatário, vício construtivo…), teste realizado, responsável, prazo, custo estimado e recomendação
- **Bloqueios de conclusão** — saída sem entrada vinculada, item obrigatório em branco, dano sem foto, checklist vazio; alertas para medidores/chaves ausentes e data fora da vigência
- **Comparação entrada × saída** item a item, com constatação (novo dano, melhoria, item removido) e classificação sugerida — sempre sujeita à validação humana
- **Laudo versionado** em HTML pronto para impressão/PDF, com número, versão, hash SHA-256 e URL de validação pública; laudo emitido nunca é sobrescrito
- **Assinatura eletrônica** — desenho em tela por link com prazo e código OTP, registrando IP, dispositivo, data/hora, geolocalização e hash vinculado ao documento; recusa registra o motivo
- **Contestações** com prazo configurável, conversa com anexos, decisão fundamentada e histórico preservado
- **Funcionamento offline** — PWA que baixa antecipadamente as vistorias em aberto: no imóvel, sem sinal, a vistoria abre e é preenchida; as alterações entram numa fila local com indicador de pendências e sobem sozinhas na reconexão
- **Permissões por função** — visualizar, criar, editar, aprovar, assinar, exportar e excluir

### Gestão
- **Painel** com métricas calculadas em tempo real, operação do dia e linha do tempo
- **Portfólio de imóveis** com busca por nome ou bairro
- **Agenda** de vistorias por status e vistoriador
- **Ocorrências** de manutenção com prioridade, prazo e custo estimado
- **Auditoria** — todo evento relevante registra ação, entidade, autor e detalhe

### Interface e acessibilidade
- **Sistema visual em tokens** (cor, escala tipográfica, espaço de 4px, elevação, movimento) num só arquivo `app.css`
- **Navegação em dois níveis**: o que se usa todo dia na barra; cadastros e documentos no menu "Mais". No celular, dock de polegar + painel agrupado por seção
- **Ícones em SVG** herdando `currentColor`, no lugar dos caracteres tipográficos que variavam por sistema
- **WCAG 2.2 AA**: contraste conferido par a par, foco visível único, atalho "ir para o conteúdo", diálogos com foco preso e devolvido, erro por campo ligado por `aria-describedby`, estado nunca só por cor, alvos de toque ≥44px
- Movimento discreto e sempre condicionado a `prefers-reduced-motion`; suporte a `forced-colors`

---

## Stack

| Camada | Tecnologia |
|---|---|
| API | ASP.NET Core Web API — .NET 10 |
| Front-end | HTML + CSS + JavaScript puro, sem framework nem build step |
| Contrato | OpenAPI em `/openapi/v1.json` |
| Persistência | Em memória (`DemoVistoraStore`) — trocável por EF Core/PostgreSQL |
| Container | Docker + Docker Compose (API + Postgres 16) |

Dependência NuGet única: `Microsoft.AspNetCore.OpenApi`. Sem CDN de JavaScript, sem `node_modules`.

---

## Executar

Requer **.NET SDK 10** (ou ajuste o `TargetFramework` e as imagens Docker para .NET 8).

```powershell
dotnet run --project .\SaasVistoria
```

Abra `http://localhost:5062` (ou `https://localhost:7093`).

**Com Docker** — copie `.env.example` para `.env` e execute:

```powershell
docker compose up --build
```

A aplicação fica em `http://localhost:8080`.

### Acesso demonstrativo

```
admin@atelierimoveis.com.br
Vistora@2026
```

> Os dados são recriados a cada inicialização — a persistência ainda é em memória.

---

## Arquitetura

Projeto único organizado em camadas por pasta, com a dependência apontando sempre para dentro:

```
Controllers  →  Application (IVistoraStore)  ←  Infrastructure (DemoVistoraStore)
                        ↓
                     Domain
```

```
SaasVistoria/
├── Domain/Models.cs                    entidades (records imutáveis) e enums
├── Application/
│   ├── Contracts.cs                    IVistoraStore, DTOs, TokenService, PasswordHasher, Permissions
│   ├── InspectionServices.cs           regras de conclusão, comparação e renderização do laudo
│   └── RequirePermission.cs            filtro de autorização por permissão granular
├── Infrastructure/
│   ├── DemoVistoraStore.cs             implementação em memória, thread-safe
│   ├── DemoVistoraStore.Fluxo.cs       partes, contratos, medidores, laudos, assinaturas, contestações
│   └── TemplateCatalog.cs              catálogo dos 14 modelos padrão
├── Pages/                              casca HTML em Razor Pages
│   ├── Index.cshtml                    "/" — aplicação
│   ├── Assinar.cshtml                  "/assinar" — assinatura pública
│   └── Shared/                         _Layout, _IconSprite, _LoginScreen,
│                                       _AppHeader, _MobileNav, _Modals
├── Controllers/
│   ├── AuthController.cs               /api/auth/*
│   ├── VistoraController.cs            imóveis, vistorias, checklist, evidências, ocorrências
│   ├── FluxoController.cs              partes, contratos, medidores, chaves, inventário, check-in, comparação
│   ├── LaudoController.cs              laudos versionados, assinaturas e contestações
│   └── PublicoController.cs            /api/publico/* — validação de laudo e assinatura por link (sem token)
├── wwwroot/                            comportamento e estilo (app.css, app.js, vistoria.js,
│                                       assinar.js, sw.js, manifest.webmanifest)
└── Program.cs                          DI, CORS, middleware de autenticação
```

**Ponto de extensão principal:** toda a superfície de dados está no contrato `IVistoraStore`. Trocar o armazenamento em memória por EF Core/PostgreSQL significa implementar essa interface e registrá-la em `Program.cs` — nenhuma outra camada precisa mudar.

Ao adicionar um endpoint, estenda os três juntos: `IVistoraStore` + `DemoVistoraStore` + o controller correspondente.

---

## API

Todas as rotas `/api/*` — exceto `/api/auth/*` — exigem token **Bearer**. Faça login e envie o `accessToken` no header `Authorization: Bearer <token>`.

| Método | Rota | Finalidade |
|---|---|---|
| `POST` | `/api/auth/login` | Sessão (JWT HS256 assinado) e refresh token |
| `POST` | `/api/auth/forgot-password` | Recuperação de senha |
| `GET` `POST` | `/api/properties` | Consultar e cadastrar imóveis (respeita limite do plano) |
| `GET` `POST` | `/api/templates` | Modelos de checklist — padrão e personalizados |
| `DELETE` | `/api/templates/{id}` | Remover modelo personalizado |
| `GET` `POST` | `/api/inspections` | Agenda e criação, opcionalmente a partir de um modelo |
| `GET` | `/api/inspections/{id}` | Detalhe de uma vistoria |
| `POST` | `/api/inspections/{id}/complete` | Enviar para revisão e recalcular conclusão |
| `GET` `POST` | `/api/inspections/{id}/items` | Listar e adicionar tópicos do checklist |
| `PUT` `DELETE` | `/api/inspections/{id}/items/{itemId}` | Atualizar condição/observação ou remover tópico |
| `GET` `POST` | `/api/inspections/{id}/evidence` | Evidências com geolocalização e hash |
| `GET` `POST` | `/api/occurrences` | Manutenção e pendências |
| `PUT` | `/api/occurrences/{id}/status` | Atualizar status da ocorrência |
| `GET` | `/api/dashboard` | Indicadores calculados, agenda e auditoria |
| `GET` | `/api/me` | Usuário da sessão e suas permissões |
| `GET` `POST` | `/api/people` | Partes (locador, locatário, fiador…) — documento mascarado na leitura |
| `GET` `POST` | `/api/contracts` | Contratos de locação vinculados a imóvel e partes |
| `GET` `POST` | `/api/inspections/{id}/meters` | Leituras de água, energia e gás |
| `GET` `POST` | `/api/inspections/{id}/keys` | Relação de chaves e controles |
| `GET` `POST` | `/api/inspections/{id}/inventory` | Inventário de imóvel mobiliado |
| `POST` | `/api/inspections/{id}/check-in` `check-out` | Registro de presença com GPS |
| `GET` | `/api/inspections/{id}/validacao` | Bloqueios e alertas antes de concluir |
| `GET` | `/api/inspections/{id}/comparacao` | Comparação entrada × saída item a item |
| `GET` `POST` | `/api/inspections/{id}/laudos` | Versões emitidas e emissão de nova versão |
| `GET` | `/api/inspections/{id}/laudos/previa` | Prévia do laudo (HTML, não selada) |
| `GET` | `/api/laudos/{id}/html` | Laudo selado com as assinaturas anexadas |
| `POST` | `/api/laudos/{id}/assinaturas/solicitar` | Convite de assinatura por link + OTP |
| `GET` `POST` `PUT` | `/api/contestacoes` | Contestações, conversa e decisão |
| `GET` | `/api/publico/laudos/{numero}` | **Sem token** — validação pública de autenticidade |
| `GET` `POST` | `/api/publico/assinaturas` | **Sem token** — assinatura por link com prazo |

### Exemplo

```bash
# 1. Autenticar
TOKEN=$(curl -s -X POST http://localhost:5062/api/auth/login \
  -H "content-type: application/json" \
  -d '{"email":"admin@atelierimoveis.com.br","password":"Vistora@2026"}' \
  | jq -r .accessToken)

# 2. Criar vistoria a partir de um modelo (gera todos os tópicos automaticamente)
curl -X POST http://localhost:5062/api/inspections \
  -H "Authorization: Bearer $TOKEN" -H "content-type: application/json" \
  -d '{"propertyId":"...","type":"Vistoria de entrada",
       "scheduledAt":"2026-09-01T10:00:00","inspector":"Ana Ribeiro",
       "templateId":"..."}'
```

---

## Segurança implementada

- **Autenticação** — JWT **HS256 assinado** (`TokenService`), validado por middleware em `Program.cs`. A chave vem de `Jwt:Key` (`docker-compose.yml` / `.env`).
- **Senhas** — **PBKDF2** com SHA-256, 120.000 iterações e salt por usuário, comparadas em tempo constante (`PasswordHasher`).
- **XSS** — todo conteúdo dinâmico do front-end passa por escaping.
- **Concorrência** — o store protege suas coleções com lock.

### Limitações conhecidas

Este ainda é um projeto em evolução. Não assuma como produção:

- Persistência **em memória** — os dados não sobrevivem a um restart (inclusive laudos e assinaturas)
- **Laudo em PDF** é gerado a partir do HTML pelo navegador (imprimir → salvar em PDF); não há PDF/A, DOCX nem QR Code impresso
- **Notificações** (e-mail, SMS, WhatsApp) não estão integradas: o link e o OTP de assinatura aparecem na própria tela
- **Offline** usa `localStorage` sem criptografia local (fila de gravações + cópia das leituras); fotos capturadas offline ainda não entram na fila
- Não implementados desta especificação: roteirização por mapa, OCR/IA de imagens, integrações (CRM, ERP, calendários), portal dedicado de locador/locatário, 2FA e exportações CSV/XLSX/DOCX
- **Multi-tenancy** não aplicada: as entidades carregam `CompanyId`, mas não há filtro por tenant (o store atende uma empresa)
- Refresh token é emitido, mas **não persistido**
- **CORS aberto** (`AllowAnyOrigin`)
- Evidências são gravadas como data URL, não em blob storage

---

## Evolução para produção

Roadmap, em ordem de prioridade:

1. **EF Core + Npgsql** implementando `IVistoraStore`, com migrations e filtro global por `CompanyId`
2. **PDF real no servidor** (hoje o laudo é HTML pronto para imprimir/salvar em PDF pelo navegador) e QR Code de validação impresso no documento
3. **Blob storage** (Azure Blob / S3) para as evidências, com URLs assinadas e expiração
4. **Assinatura ICP-Brasil** via provedor externo, além da assinatura eletrônica avançada já implementada
5. Refresh token persistido, 2FA, rate limiting e CORS restrito
6. Segredos em cofre (Key Vault / Secrets Manager)
7. Observabilidade com OpenTelemetry / Serilog
8. Testes de integração com PostgreSQL efêmero

---

## Convenções do código

- C# com `Nullable` e `ImplicitUsings` habilitados
- Estilo terso: actions expression-bodied, primary constructors, collection expressions `[...]`
- Entidades são `record`s imutáveis, concentrados em `Domain/Models.cs`
- Enums serializados como string via `JsonStringEnumConverter`
- Domínio e interface em **português brasileiro** — mantenha textos novos em pt-BR
