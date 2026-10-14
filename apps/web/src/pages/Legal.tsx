import { Link } from "react-router-dom";
import { AuthLayout } from "./Auth";

const AVISO = "MINUTA — este texto é um modelo inicial e precisa ser revisado por advogado(a) antes da publicação do serviço.";

function Pagina({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return (
    <AuthLayout titulo={titulo} subtitulo="Versão 1.0">
      <p className="rounded-xl bg-warning-soft p-3 text-xs font-medium text-warning">{AVISO}</p>
      <div className="space-y-4 text-sm leading-relaxed [&_h2]:mt-2 [&_h2]:text-base [&_h2]:font-semibold [&_li]:ml-5 [&_li]:list-disc">{children}</div>
      <p className="text-center text-sm"><Link to="/cadastro" className="font-semibold text-primary">Voltar ao cadastro</Link></p>
    </AuthLayout>
  );
}

export function Termos() {
  return (
    <Pagina titulo="Termos de Uso">
      <h2>1. O que é o CoffeeJá</h2>
      <p>Plataforma que conecta contratantes de coffee break corporativo a buffets e fornecedores, para solicitação e comparação de propostas, negociação e acompanhamento de pedidos. O CoffeeJá intermedia o contato; o serviço de alimentação é prestado pelo buffet contratado.</p>
      <h2>2. Cadastro e conta</h2>
      <p>As informações cadastrais devem ser verdadeiras e atualizadas. O administrador da conta empresarial responde pelos acessos que criar e pelos pedidos feitos por sua equipe em nome da empresa. Credenciais são pessoais e intransferíveis.</p>
      <h2>3. Pedidos, pagamento e notas fiscais</h2>
      <p>Neste momento o pagamento é combinado diretamente entre contratante e buffet, fora da plataforma. A nota fiscal deve ser anexada ao pedido. As regras de pagamento pela plataforma serão informadas antes de entrarem em vigor.</p>
      <h2>4. Conduta</h2>
      <p>É vedado usar a plataforma para fraudes, informações falsas, assédio, tentativa de acesso indevido ou para contornar a intermediação de forma abusiva. Contas podem ser suspensas em caso de violação.</p>
      <h2>5. Responsabilidades</h2>
      <p>O CoffeeJá empenha-se em manter o serviço disponível e seguro, sem garantia de funcionamento ininterrupto. A qualidade, a entrega e a regularidade fiscal do serviço de alimentação são de responsabilidade do buffet.</p>
      <h2>6. Alterações</h2>
      <p>Estes termos podem ser atualizados; mudanças relevantes serão comunicadas e, quando necessário, um novo aceite será solicitado.</p>
    </Pagina>
  );
}

export function Privacidade() {
  return (
    <Pagina titulo="Política de Privacidade">
      <h2>1. Quem é o controlador</h2>
      <p>CoffeeJá (razão social, CNPJ e canal do encarregado de dados a definir). Contato sobre privacidade: <b>privacidade@coffeeja.com.br</b> (endereço a confirmar).</p>
      <h2>2. Dados que coletamos</h2>
      <ul>
        <li>Cadastro: nome, CPF, e-mail, telefones e WhatsApp, cargo/setor, dados e endereço da empresa ou da pessoa, endereço para nota fiscal.</li>
        <li>Uso: demandas, propostas, mensagens do chat, pedidos, notas fiscais anexadas e avaliações.</li>
        <li>Segurança: endereço IP, navegador e registros de acesso e de ações sensíveis.</li>
      </ul>
      <h2>3. Para que usamos</h2>
      <p>Executar o serviço de intermediação, identificar e autenticar usuários, registrar quem solicitou e contratou cada pedido, prevenir fraudes, cumprir obrigações legais e fiscais e, com sua indicação, enviar convites por WhatsApp.</p>
      <h2>4. Com quem compartilhamos</h2>
      <p>Buffets e contratantes envolvidos em um pedido veem os dados necessários à contratação (o endereço exato do evento só é liberado ao buffet contratado). Usamos provedores de infraestrutura (hospedagem), de geocodificação de endereços e de consulta de CEP. Não vendemos dados pessoais.</p>
      <h2>5. Por quanto tempo</h2>
      <p>Mantemos os dados enquanto a conta estiver ativa e, depois, pelo prazo necessário para cumprir obrigações legais, fiscais e para defesa em processos.</p>
      <h2>6. Seus direitos (LGPD, art. 18)</h2>
      <p>Confirmação de tratamento, acesso, correção, anonimização ou eliminação, portabilidade, informação sobre compartilhamento e revogação de consentimento. Em <b>Conta → Segurança e privacidade</b> você baixa seus dados e solicita a exclusão.</p>
      <h2>7. Segurança</h2>
      <p>Adotamos criptografia em trânsito, senhas armazenadas com hash, sessões protegidas, verificação em duas etapas, limites contra abuso e registro de auditoria.</p>
      <h2>8. Cookies</h2>
      <p>Usamos apenas o cookie essencial de sessão (sem rastreamento publicitário).</p>
    </Pagina>
  );
}
