# Segurança e escalabilidade — CoffeeJá

Este documento resume o que já está implementado, o que falta antes de homologação/produção e o que preparar para escalar. A marcação indica: ✅ feito e testado · 🟡 parcialmente · ⬜ pendente.

## 1. Implementado (e como foi verificado)

Verificação: 36 testes unitários (inclui o vetor oficial do RFC 6238) + roteiro automatizado de ~65 cenários de segurança contra a API real + teste de interface do 2FA.

| Área | O que existe |
|---|---|
| **Sessão** ✅ | JWT em **cookie HttpOnly + SameSite=Strict + Secure** (o JavaScript não lê o token, o que limita o dano de um XSS). Validade 8 h. A cada requisição confere se o usuário segue ativo, se a conta não foi suspensa e se o *security stamp* não mudou. Trocar a senha, desativar o usuário ou ativar o 2FA **derrubam as demais sessões**. O token nunca vai na URL. |
| **CSRF** ✅ | `SameSite=Strict` + cabeçalho obrigatório `X-Requested-With` em toda escrita feita por cookie (testado: sem o cabeçalho → 403). |
| **Login** ✅ | Bloqueio após 5 erros (15 min), resposta igual para usuário inexistente (inclui tempo gasto), limite por IP, mensagens genéricas. |
| **Senhas** ✅ | Hash PBKDF2 (ASP.NET Identity). Política: mínimo 10, letras + número/símbolo, sem senhas comuns, sem nome/e-mail do usuário (NIST 800-63B). Vale para cadastro, troca e primeiro acesso. |
| **2FA (TOTP)** ✅ | Aplicativo autenticador + 8 códigos de recuperação de uso único; segredo cifrado em repouso (Data Protection); anti-replay do código. **Obrigatório para o administrador do CoffeeJá** (sem 2FA o painel retorna 403). Opcional para os demais. |
| **Autorização** ✅ | Políticas por perfil (Contratante, Fornecedor, Admin) e por função (admin da conta); todos os acessos a demanda/proposta/pedido/NF/chat são filtrados pela conta do token (testado contra IDOR). Endereço exato só para o buffet contratado; contatos ocultos no chat até o fechamento. |
| **Links públicos** ✅ | Aprovação do superior e primeiro acesso: token aleatório de 256 bits, **só o hash fica no banco**, uso único, expira em 7 dias, CPF mascarado. |
| **Upload de NF** ✅ | Extensão + **assinatura real do arquivo** (magic bytes), limite 10 MB, nome aleatório fora da pasta pública, download como anexo com `nosniff`, limite de 10 uploads/min. |
| **Cabeçalhos HTTP** ✅ | `nosniff`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, COOP/CORP, CSP restritiva na API, `Cache-Control: no-store` nos dados, HSTS em produção, header `Server` removido. CSP do front no `deploy/Caddyfile`. |
| **Abuso / DoS** ✅ | Limite global por usuário/IP (600/min), limites específicos (login, chat 20/min, upload, geocodificação), corpo máximo 12 MB (JSON 256 KB), timeouts de cabeçalho, listas com teto. Só confia em `X-Forwarded-For` dos proxies configurados. |
| **Auditoria** ✅ | Tabela `LogsAuditoria` (login ok/falha/bloqueio, troca de senha, 2FA, criação/desativação de usuários, aprovações, suspensões, NF anexada/baixada, LGPD) com IP e navegador; consulta e filtro no painel admin. |
| **LGPD** ✅ | Aceite dos Termos/Privacidade com versão e data; **exportar meus dados**; **solicitar exclusão** → fila no admin → **anonimização** (mantém registros fiscais). Minutas de Termos e Política em `/termos` e `/privacidade` (**precisam de revisão jurídica**). |
| **Configuração** ✅ | Em produção a API **recusa subir** com chave JWT de desenvolvimento, origens localhost, dados demo, senha de admin padrão ou aprovação automática. Segredos só por variável de ambiente (`deploy/api.env.example`). |
| **Cadeia de suprimentos** ✅ | GitHub Actions: build/testes, `dotnet list package --vulnerable`, `npm audit`, **CodeQL**, **gitleaks**; Dependabot semanal. `SECURITY.md` com canal de divulgação. |
| **Servidor** ✅ | `deploy/coffeeja-api.service` (systemd endurecido: usuário sem privilégios, FS somente leitura, sem capabilities) e `deploy/Caddyfile` (HTTPS automático, CSP, limite de corpo). |

## 2. Pendente antes da homologação / produção

Itens que dependem de infraestrutura, contas externas ou decisão sua.

| Prioridade | Item | Observação |
|---|---|---|
| Alta ⬜ | **HTTPS + domínio** | Sem HTTPS o cookie `Secure` e o HSTS não funcionam. Caddy cuida do certificado. |
| Alta ⬜ | **Recuperação de senha por e-mail** | Exige serviço de e-mail transacional (SES, Postmark, Resend…) + SPF/DKIM/DMARC. Hoje o admin/Conta redefine; falta o "esqueci minha senha". |
| Alta ⬜ | **Verificação de e-mail** no cadastro | Mesmo pré-requisito de e-mail. |
| Alta ⬜ | **Backups cifrados e testados** | Banco + `/var/coffeeja` (**notas fiscais e chaves do Data Protection** — sem as chaves os segredos 2FA não abrem). Restaurar de teste a cada trimestre. |
| Alta ⬜ | **Usuário do SQL Server com privilégio mínimo** | A aplicação não deve ser `sa`/`db_owner`. Rodar migrations por um job separado (`Banco__MigrarNaInicializacao=false`). |
| Alta ⬜ | **Revisão jurídica** dos Termos/Política + nomear o encarregado (DPO) e o canal de privacidade | Os textos atuais são minutas. |
| Média ⬜ | **Criptografia de colunas sensíveis (CPF/CNPJ)** | Hoje protegidos por controle de acesso. Opções: TDE/Always Encrypted do SQL Server, ou cifrar a coluna + índice cego (HMAC) para busca. Decidir conforme o edital/risco. |
| Média ⬜ | **Antivírus nos uploads** (ClamAV) | Mitiga arquivos maliciosos em NF; hoje há validação de assinatura. |
| Média ⬜ | **WAF / proteção DDoS** | Cloudflare (gratuito) na frente do Hetzner, com regras de rate limit. |
| Média ⬜ | **Firewall do servidor** | Só 80/443 (e SSH por chave, com fail2ban); SQL Server sem exposição pública. |
| Média ⬜ | **Monitoramento e alertas** | Falhas de login em massa, 5xx, certificado, disco, backup. |
| Média ⬜ | **Teste de invasão** externo | Antes do lançamento público. |
| Baixa 🟡 | **Self-host da fonte Inter** | Remove a chamada ao Google Fonts (privacidade/CSP mais estrita). |
| Baixa ⬜ | **Antifraude de cadastro de buffet** | Conferir CNPJ na Receita (API) e checar situação cadastral. |
| Baixa ⬜ | **Política de retenção automática** | Job que apaga/anonimiza dados após o prazo definido pelo jurídico. |

## 3. Escalabilidade

Estado atual: API **sem estado** (sessão no token), pronta para várias instâncias atrás de balanceador, com índices nas colunas de consulta e listas limitadas. O que mudar, em ordem de necessidade:

| Gatilho | O que fazer | Hoje |
|---|---|---|
| Mais de 1 instância da API | **Redis** como backplane do SignalR, cache distribuído de sessão (`SessaoCache`, hoje em memória/15 s) e limitador de taxa compartilhado | ⬜ (design já isola o cache em `SessaoCache`) |
| Mais de 1 instância | **Armazenamento de arquivos em objeto** (Hetzner Object Storage/S3) no lugar da pasta local (`IArquivoStorage` já é interface) + **chaves do Data Protection** em armazenamento compartilhado | ⬜ |
| Picos de notificação/push | **Fila de trabalhos** (Hangfire/RabbitMQ) e padrão *outbox*: hoje o push sai em `Task.Run` dentro da API | ⬜ |
| Banco sob carga | Índices já existem (status/data, lat/long, usuário); depois: réplica de leitura para dashboards, cache de contagens, paginação por cursor nas listas | 🟡 |
| Busca por raio em muitos buffets | Trocar caixa+Haversine por **tipo `geography` + índice espacial** do SQL Server | ⬜ |
| Observabilidade | **OpenTelemetry** + logs estruturados (Serilog → Seq/Loki) e *health checks* (`/health/ready` já valida o banco) | 🟡 |
| Entrega contínua | Build de imagem/artefato, deploy *blue-green* com migração prévia, *feature flags* | ⬜ |
| Pagamento na plataforma | Provider de PSP com split/escrow + webhooks idempotentes (`IPagamentoProvider`) | ⬜ |
| Notificações multicanal | WhatsApp Business API / e-mail via `INotificador` | ⬜ |
| Multi-região/cidades | Particionar demandas por região, CDN para o front, *rate limits* por tenant | ⬜ |

## 4. Mapa rápido OWASP Top 10 (2021)

A01 Controle de acesso → políticas + filtro por conta + testes IDOR · A02 Falhas criptográficas → HTTPS/HSTS, hash de senha, segredos TOTP cifrados, tokens com hash (CPF em repouso: pendente) · A03 Injeção → EF Core parametrizado, React escapa saída, CSP · A04 Design inseguro → modelo de ameaças por perfil, aprovação do superior, 2FA do admin · A05 Configuração → guarda de produção, cabeçalhos, systemd endurecido · A06 Componentes vulneráveis → Dependabot/CodeQL/audit · A07 Autenticação → bloqueio, 2FA, política de senha, sessões revogáveis · A08 Integridade → CI, assinatura de arquivo no upload · A09 Logs/monitoramento → auditoria + (alertas pendentes) · A10 SSRF → a API só chama hosts fixos (Nominatim, push); nenhum destino vem do usuário.
