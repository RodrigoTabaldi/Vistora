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

### Gestão
- **Painel** com métricas calculadas em tempo real, operação do dia e linha do tempo
- **Portfólio de imóveis** com busca por nome ou bairro
- **Agenda** de vistorias por status e vistoriador
- **Ocorrências** de manutenção com prioridade, prazo e custo estimado
- **Auditoria** — todo evento relevante registra ação, entidade, autor e detalhe

### Interface
- Responsiva de verdade: barra superior com indicador animado no desktop, menu sanduíche e dock inferior no celular
- Tipografia ampliada (base 17px) pensada para uso sob sol, em pé, com uma mão
- Respeita `prefers-reduced-motion`

---

## Stack

| Camada | Tecnologia |
|---|---|
| API | ASP.NET Core Web API — .NET 10 |
| Front-end | HTML + CSS + JavaScript puro, sem framework nem build step |
| Contrato | OpenAPI em `/openapi/v1.json` |
| Persistência | Em memória (`DemoVistoraStore`) — trocável por EF Core/PostgreSQL |
| Container | Docker + Docker Compose (API + Postgres 16) |

Dependências NuGet diretas: `Microsoft.AspNetCore.OpenApi` e `Microsoft.OpenApi` (fixada em versão corrigida — ver `SaasVistoria.csproj`). Sem CDN de JavaScript, sem `node_modules`.

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
├── Application/Contracts.cs            IVistoraStore, DTOs, TokenService, PasswordHasher
├── Infrastructure/
│   ├── DemoVistoraStore.cs             implementação em memória, thread-safe
│   └── TemplateCatalog.cs              catálogo dos 14 modelos padrão
├── Controllers/
│   ├── AuthController.cs               /api/auth/*
│   ├── VistoraApiControllerBase.cs     base com CurrentActor e paginação (PagedOk)
│   ├── DashboardController.cs          /api/dashboard
│   ├── PropertiesController.cs         /api/properties
│   ├── TemplatesController.cs          /api/templates
│   ├── InspectionsController.cs        /api/inspections/* (itens, evidências)
│   └── OccurrencesController.cs        /api/occurrences/*
├── wwwroot/                            front-end (index.html, app.js, app.css)
└── Program.cs                          DI, CORS, rate limiting, middleware de autenticação
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

- **Autenticação** — JWT **HS256 assinado** (`TokenService`), validado por middleware em `Program.cs`. A chave vem de `Jwt:Key` (`docker-compose.yml` / `.env`); fora do ambiente `Development`, a aplicação **falha ao iniciar** se `Jwt:Key` não estiver configurada com pelo menos 32 caracteres — nunca existe uma chave padrão gravada no código-fonte. Em `Development` sem `Jwt:Key`, uma chave aleatória é gerada só para aquela execução (aviso no console; sessões não sobrevivem a um restart).
- **Rate limiting no login** — `/api/auth/login` aceita no máximo 5 tentativas por minuto por IP (`RateLimiter` nativo do ASP.NET Core).
- **Papéis/permissões** — `RequireRoleAttribute` bloqueia com 403 endpoints administrativos (cadastro de imóvel, criação/remoção de modelo) para usuários fora do papel `Administrador`.
- **Senhas** — **PBKDF2** com SHA-256, 120.000 iterações e salt por usuário, comparadas em tempo constante (`PasswordHasher`).
- **Evidências com hash verificável** — o hash SHA-256 de cada foto é calculado só sobre os bytes da imagem (nunca sobre um timestamp) e armazenado por inteiro, então pode ser recalculado e conferido depois contra o mesmo arquivo.
- **XSS** — todo conteúdo dinâmico do front-end passa por escaping.
- **Concorrência** — o store protege suas coleções com lock.
- **Testes automatizados** — `SaasVistoria.Tests` (xUnit) cobre `PasswordHasher`, `TokenService`, `RequireRoleAttribute` e a lógica de conclusão/pendências/hash do `DemoVistoraStore`; roda em CI (`.github/workflows/ci.yml`) a cada push/PR.

### Limitações conhecidas

Este ainda é um projeto em evolução. Não assuma como produção:

- Persistência **em memória** — os dados não sobrevivem a um restart
- **Multi-tenancy** não aplicada: as entidades carregam `CompanyId`, mas não há filtro por tenant (o store atende uma empresa)
- Refresh token é emitido, mas **não persistido**
- **CORS aberto** (`AllowAnyOrigin`)
- Evidências são gravadas como data URL, não em blob storage
- Token de sessão fica em `localStorage` no front-end (não em cookie `HttpOnly`)
- Só existe um papel de usuário nos dados de seed (`Administrador`) — o mecanismo de permissões (`RequireRoleAttribute`) está pronto, mas ainda não há um segundo papel para validar contra ele

---

## Evolução para produção

Roadmap, em ordem de prioridade:

1. **EF Core + Npgsql** implementando `IVistoraStore`, com migrations e filtro global por `CompanyId`
2. **Laudo em PDF** com fotos, hashes e assinatura digital das partes
3. **Blob storage** (Azure Blob / S3) para as evidências
4. **Comparativo entrada × saída** — o relatório que fecha o ciclo da locação
5. Refresh token persistido e CORS restrito
6. Segredos em cofre (Key Vault / Secrets Manager)
7. Observabilidade com OpenTelemetry / Serilog
8. Testes de integração com PostgreSQL efêmero
9. Cookie `HttpOnly`/`SameSite=Strict` para o token de sessão, no lugar de `localStorage`

---

## Convenções do código

- C# com `Nullable` e `ImplicitUsings` habilitados
- Estilo terso: actions expression-bodied, primary constructors, collection expressions `[...]`
- Entidades são `record`s imutáveis, concentrados em `Domain/Models.cs`
- Enums serializados como string via `JsonStringEnumConverter`
- Domínio e interface em **português brasileiro** — mantenha textos novos em pt-BR
