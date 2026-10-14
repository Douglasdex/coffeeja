# CoffeeJá

Plataforma que conecta **contratantes** de coffee break corporativo a **buffets**. O contratante publica a demanda; os buffets num raio de **50 km** recebem aviso e enviam cotações; o contratante compara, pede ajustes, aceita e acompanha o pedido. O pagamento é feito **fora da plataforma** (por enquanto), com **anexo de Nota Fiscal** no pedido.

## Estrutura (monorepo)

```
apps/
  api/                 .NET 10 · ASP.NET Core · EF Core · SQL Server (monólito modular)
    src/CoffeeJa.Domain          entidades, enums, cálculo geográfico
    src/CoffeeJa.Infrastructure  DbContext + migrations
    src/CoffeeJa.Api             endpoints (Features/), serviços, Program.cs
    tests/CoffeeJa.Tests         testes xUnit
  web/                 React 19 · Vite · TypeScript · Tailwind 4 · PWA (mobile-first)
packages/
  ui/                  design system (tokens da referência + componentes estilo shadcn)
  api-client/          cliente tipado da API (tipos espelham os DTOs)
```

## Requisitos

- .NET SDK 10, Node 22+
- SQL Server (em desenvolvimento usa **LocalDB**: `(localdb)\MSSQLLocalDB`)

## Rodando em desenvolvimento

Copie `apps/api/src/CoffeeJa.Api/appsettings.Development.example.json` para `appsettings.Development.json` (ignorado pelo git; pode conter chaves VAPID de teste).

```bash
npm install
npm run dev:api     # API em http://localhost:5080 (aplica migrations e cria o admin de dev)
npm run dev         # Web em http://localhost:5173 (proxy /api → 5080)
npm run test:api    # testes do backend
```

Admin de desenvolvimento: `admin@coffeeja.local` / `Admin@12345` (só em `appsettings.Development.json`).
Em desenvolvimento fornecedores são aprovados automaticamente; em produção o admin aprova em `POST /api/admin/fornecedores/{id}/aprovar`.

### Migrations

```bash
dotnet ef migrations add NomeDaMudanca -p apps/api/src/CoffeeJa.Infrastructure -s apps/api/src/CoffeeJa.Api -o Migrations
```

## Fluxo implementado

1. **Contratante** cria demanda (wizard de 4 passos) → fornecedores aprovados em até 50 km são notificados.
2. **Fornecedor** vê as demandas do raio, responde em poucos toques (cardápios-base) e envia proposta.
3. **Contratante** compara propostas (ranking por custo-benefício: 60% preço, 40% nota), conversa, **pede ajuste**; o fornecedor reenvia nova versão; **aceite final** gera o **pedido**.
4. **Pedido**: linha do tempo (preparo → a caminho → entregue → concluído), **nota fiscal** (PDF/XML/imagem, 10 MB), avaliação do fornecedor.

Regras de proteção: endereço exato só para quem foi contratado; telefone/e-mail são mascarados no chat até o fechamento (evita desintermediação).

## Pagamento (preparado para evoluir)

`IPagamentoProvider` (`Services/Servicos.cs`) abstrai o meio de pagamento. Hoje `PagamentoManualProvider` marca o pedido como *pago fora da plataforma*. Para ligar escrow + split (Pagar.me, Asaas, Mercado Pago…), implemente um novo provider e troque o registro no `Program.cs`. `Pedido` já guarda `ComissaoPercentual` e `StatusPagamento` (Pendente/Retido/Liberado/Reembolsado).

## Produção (Hetzner)

Configuração por variáveis de ambiente (nunca no repositório):

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Server=...;Database=CoffeeJa;User Id=...;Password=...;TrustServerCertificate=True
Jwt__Key=<mínimo 32 caracteres aleatórios>
Plataforma__Origens__0=https://seu-dominio
Plataforma__PastaArquivos=/var/coffeeja/storage      # notas fiscais — inclua no backup
Plataforma__AdminEmail=...  Plataforma__AdminSenha=...   # cria o 1º admin
Banco__MigrarNaInicializacao=true                        # aplica migrations ao subir
```

Sugestão de topologia no servidor: **Caddy** (HTTPS automático) servindo `apps/web/dist` e fazendo proxy de `/api` para o Kestrel (`dotnet publish apps/api/src/CoffeeJa.Api -c Release`, rodando como serviço systemd). A API já respeita `X-Forwarded-For/Proto`.

## Tempo real, push e admin

- **Chat em tempo real:** SignalR (`/hubs/chat`). A escrita segue pela API REST; o servidor apenas avisa os participantes (o front invalida o cache). Há polling de 60 s como rede de segurança.
- **Web Push:** `PosSalvarInterceptor` envia push para cada notificação gravada. Gere chaves VAPID (`Push__PublicKey`, `Push__PrivateKey`, `Push__Subject`); sem chaves o push fica desligado e a opção some do app. No iPhone só funciona com o app instalado na tela inicial (HTTPS obrigatório).
- **Admin** (`/admin`): resumo da plataforma, aprovar/suspender buffets e selo Fundador. O 1º admin é criado por `Plataforma__AdminEmail/AdminSenha`.
- Em produção o proxy precisa liberar WebSocket em `/hubs` (Caddy faz isso por padrão).

## Próximos passos sugeridos

- WhatsApp/e-mail como canais extras de notificação.
- Recorrência de demandas, nota fiscal consolidada, escrow + split.
- Geocodificação: o endpoint usa Nominatim (uso leve). Em escala, trocar por Google/Mapbox.
