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

```bash
npm install
npm run setup      # cria appsettings.Development.json, restaura pacotes .NET, checa o LocalDB
npm run dev:all    # API (http://localhost:5080) + Web (http://localhost:5173) juntos
```

Abra **http://localhost:5173**. Na primeira execução a API cria o banco `CoffeeJa` no LocalDB e carrega dados de demonstração.

| Perfil | E-mail | Senha |
|---|---|---|
| Empresa — administrador | contratante@demo.local | Demo@12345 |
| Empresa — funcionário (login pelo CPF) | 111.444.777-35 | Demo@12345 |
| Pessoa física | pf@demo.local | Demo@12345 |
| Buffet (fundador, atende vegano/sem glúten) | buffet1@demo.local | Demo@12345 |
| Buffet | buffet2@demo.local | Demo@12345 |
| Buffet em Campinas (fora do raio de 50 km) | buffet3@demo.local | Demo@12345 |
| Admin | admin@coffeeja.local | Admin@12345 |

Dica: abra duas janelas (uma anônima) — contratante em uma, buffet na outra — para ver propostas, chat e notificações em tempo real.

Outros comandos: `npm run dev:api`, `npm run dev`, `npm run test:api`, `npm run typecheck`, `npm run db:reset` (apaga o banco; é recriado ao subir a API).
Para outro SQL Server, ajuste `ConnectionStrings:Default` em `appsettings.Development.json`. Para não carregar dados demo, use `"Banco": { "DadosDemo": false }`.

Em desenvolvimento fornecedores são aprovados automaticamente; em produção o admin aprova em `/admin`.

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

## Contas e perfis

- **Empresa (PJ):** o administrador cria a conta com dados da empresa (razão social, fantasia, CNPJ, endereço) e do responsável (nome, CPF, telefone comercial e WhatsApp). Ele cadastra **funcionários** (nome, CPF, setor, telefones, superior imediato). O **superior aprova por link no WhatsApp** (página pública, uso único); depois o admin envia o acesso e o funcionário cria a senha. Login por e-mail ou **CPF**. Cada demanda/pedido registra **quem solicitou e quem contratou** (nome, setor, data e hora); a contratação é em nome da empresa.
- **Pessoa física:** dados pessoais, endereço e endereço da nota fiscal (mesmo ou outro). Todos os campos são obrigatórios.
- **Buffet:** mesmos dados da empresa; botão **"Gerar outro acesso"** (nome, CPF, cargo, telefone). Hoje gratuito; a licença paga entra depois.
- **Convites:** qualquer perfil convida por WhatsApp (botão de compartilhar no topo). O texto destaca as vantagens do app. O admin vê o **ranking de quem mais convidou**.
- **WhatsApp:** os envios abrem o WhatsApp com a mensagem pronta (`wa.me`). O disparo 100% automático exige a API oficial do WhatsApp Business (ponto de integração futuro).
- Configure `Plataforma__UrlPublica` em produção (os links enviados usam esse endereço).

> Se você já tinha um banco local de versões anteriores, rode `npm run db:reset` uma vez (o modelo de contas mudou).

## Segurança

Resumo em [docs/SEGURANCA.md](docs/SEGURANCA.md) (o que está feito, o que falta antes de produção e o plano de escalabilidade). Pontos que afetam o uso no dia a dia:

- A sessão fica em **cookie HttpOnly**; senhas exigem **10+ caracteres**; 5 erros de login bloqueiam a conta por 15 min.
- O **administrador do CoffeeJá precisa ativar a verificação em duas etapas** (app autenticador) no primeiro acesso.
- Em produção a API não sobe com configuração de desenvolvimento (veja `deploy/api.env.example`, `deploy/Caddyfile`, `deploy/coffeeja-api.service`).
- Vulnerabilidades: veja [SECURITY.md](SECURITY.md).

## Tempo real, push e admin

- **Chat em tempo real:** SignalR (`/hubs/chat`). A escrita segue pela API REST; o servidor apenas avisa os participantes (o front invalida o cache). Há polling de 60 s como rede de segurança.
- **Web Push:** `PosSalvarInterceptor` envia push para cada notificação gravada. Gere chaves VAPID (`Push__PublicKey`, `Push__PrivateKey`, `Push__Subject`); sem chaves o push fica desligado e a opção some do app. No iPhone só funciona com o app instalado na tela inicial (HTTPS obrigatório).
- **Admin** (`/admin`): resumo da plataforma, aprovar/suspender buffets e selo Fundador. O 1º admin é criado por `Plataforma__AdminEmail/AdminSenha`.
- Em produção o proxy precisa liberar WebSocket em `/hubs` (Caddy faz isso por padrão).

## Próximos passos sugeridos

- WhatsApp/e-mail como canais extras de notificação.
- Recorrência de demandas, nota fiscal consolidada, escrow + split.
- Geocodificação: o endpoint usa Nominatim (uso leve). Em escala, trocar por Google/Mapbox.
