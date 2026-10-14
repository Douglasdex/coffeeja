import type { Papel } from "@coffeeja/api-client";
import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { Carregando } from "./components/common";
import { rotaInicial, useAuth } from "./lib/auth";
import { Cadastro, Login } from "./pages/Auth";
import { Conta, ContratanteDemanda, ContratanteDemandas, ContratanteInicio, NovaDemanda } from "./pages/Contratante";
import { FornecedorDemanda, FornecedorDemandas, FornecedorInicio, FornecedorPerfil, FornecedorPropostas } from "./pages/Fornecedor";
import { AdminPainel } from "./pages/Admin";
import { Convidar } from "./pages/Convidar";
import { Equipe } from "./pages/Equipe";
import { AprovarFuncionario, AtivarAcesso } from "./pages/Publico";
import { Notificacoes } from "./pages/Notificacoes";
import { PedidoDetalhe, PedidosLista } from "./pages/Pedidos";

function Protegida({ papel, children }: { papel?: Papel; children: ReactNode }) {
  const { usuario, carregando } = useAuth();
  if (carregando) return <Carregando />;
  if (!usuario) return <Navigate to="/login" replace />;
  if (papel && usuario.papel !== papel) return <Navigate to={rotaInicial(usuario)} replace />;
  return <>{children}</>;
}

function Raiz() {
  const { usuario, carregando } = useAuth();
  if (carregando) return <Carregando />;
  return <Navigate to={usuario ? rotaInicial(usuario) : "/login"} replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Raiz />} />
      <Route path="/login" element={<Login />} />
      <Route path="/cadastro" element={<Cadastro />} />
      <Route path="/aprovar/:token" element={<AprovarFuncionario />} />
      <Route path="/ativar/:token" element={<AtivarAcesso />} />

      <Route element={<Protegida><AppShell /></Protegida>}>
        <Route path="/notificacoes" element={<Notificacoes />} />
        <Route path="/convidar" element={<Convidar />} />
      </Route>

      <Route path="/contratante" element={<Protegida papel="Contratante"><AppShell /></Protegida>}>
        <Route index element={<ContratanteInicio />} />
        <Route path="demandas" element={<ContratanteDemandas />} />
        <Route path="demandas/:id" element={<ContratanteDemanda />} />
        <Route path="nova" element={<NovaDemanda />} />
        <Route path="pedidos" element={<PedidosLista />} />
        <Route path="pedidos/:id" element={<PedidoDetalhe />} />
        <Route path="conta" element={<Conta />} />
        <Route path="equipe" element={<Equipe />} />
      </Route>

      <Route path="/fornecedor" element={<Protegida papel="Fornecedor"><AppShell /></Protegida>}>
        <Route index element={<FornecedorInicio />} />
        <Route path="demandas" element={<FornecedorDemandas />} />
        <Route path="demandas/:id" element={<FornecedorDemanda />} />
        <Route path="propostas" element={<FornecedorPropostas />} />
        <Route path="pedidos" element={<PedidosLista />} />
        <Route path="pedidos/:id" element={<PedidoDetalhe />} />
        <Route path="perfil" element={<FornecedorPerfil />} />
      </Route>

      <Route path="/admin" element={<Protegida papel="Admin"><AppShell /></Protegida>}>
        <Route index element={<AdminPainel />} />
      </Route>

      <Route path="*" element={<Raiz />} />
    </Routes>
  );
}
