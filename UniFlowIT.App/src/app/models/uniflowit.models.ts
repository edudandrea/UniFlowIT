export type Perfil = 'Usuario' | 'Atendente' | 'Administrador' | 'AdministradorSaas';
export type AuthMode = 'login' | 'bootstrap';
export type Pagina =
  | 'dashboard'
  | 'saas-dashboard'
  | 'chamados'
  | 'chamados-desenvolvedor'
  | 'cadastro-empresas'
  | 'cadastro-usuarios'
  | 'cadastro-categorias'
  | 'conhecimento'
  | 'equipamentos'
  | 'links-dashboard'
  | 'dados-empresariais'
  | 'financeiro-assinaturas'
  | 'financeiro-cobrancas'
  | 'financeiro-planos'
  | 'financeiro-pagamentos'
  | 'financeiro-despesas'
  | 'saas-implantacoes'
  | 'saas-inadimplencia'
  | 'saas-chamados-globais'
  | 'saas-sla-plataforma'
  | 'saas-incidentes'
  | 'saas-monitoramento'
  | 'saas-agentes'
  | 'saas-integracoes'
  | 'saas-acesso-remoto'
  | 'saas-relatorios'
  | 'saas-metricas-uso'
  | 'saas-auditoria'
  | 'saas-configuracoes'
  | 'saas-seguranca'
  | 'saas-administradores';
export type EmpresaTab = 'pesquisa' | 'detalhes';
export type StatusChamado = 'Aberto' | 'Em atendimento' | 'Aguardando retorno' | 'Encerrado' | 'Cancelado';
export type Prioridade = 'Baixa' | 'Media' | 'Alta' | 'Urgente';
export type StatusConhecimento = 'Publicado' | 'Em revisão' | 'Rascunho' | 'Arquivado';

export interface Sessao {
  id: number;
  nome: string;
  email: string;
  role: Perfil;
  empresaId?: number;
  empresaNome?: string;
  tenantSlug?: string;
  token: string;
}

export interface Mensagem {
  id?: number;
  autor: string;
  perfil: Perfil;
  texto: string;
  horario: string;
  enviadoEm?: string;
  timestamp?: number;
  lida?: boolean;
  tipo?: 'Chat' | 'Mural';
}

export interface Chamado {
  id: number;
  empresaId?: number;
  solicitanteUsuarioId?: number;
  numero: string;
  titulo?: string;
  solicitante: string;
  categoria: string;
  subcategoria: string;
  tipo: 'Incidente' | 'Solicitacao' | 'Alteracao';
  prioridade: Prioridade;
  status: StatusChamado;
  descricao: string;
  equipamento: string;
  anexos: string[];
  anexosDetalhes?: AnexoChamado[];
  atendente?: string;
  origem?: string;
  criadoEm?: string;
  atualizadoEm?: string;
  mensagens: Mensagem[];
  avaliacao?: number;
  avaliacaoComentario?: string;
}

export interface Artigo {
  id?: number;
  empresaId?: number;
  titulo: string;
  categoria: string;
  resumo: string;
  descricao?: string;
  tags: string[];
  anexos?: string[];
  usuario?: string;
  usuarioId?: number;
  publico?: boolean;
  status?: StatusConhecimento;
}

export interface AnexoChamado {
  id?: number;
  nome: string;
  url?: string;
  tipo?: string;
  tamanhoBytes?: number;
}

export interface CategoriaConhecimento {
  id?: number;
  empresaId?: number;
  nome: string;
  ativo: boolean;
}

export interface ConhecimentoForm {
  id?: number;
  titulo: string;
  categoria: string;
  descricao: string;
  anexos: string[];
  usuario: string;
  usuarioId?: number;
  publico: boolean;
  status: StatusConhecimento;
}

export interface CategoriaConhecimentoForm {
  id?: number;
  nomeOriginal?: string;
  nome: string;
  ativo: boolean;
}

export interface EnvioEquipamento {
  empresaId?: number;
  patrimonio: string;
  tipo: string;
  filialDestino: string;
  responsavel: string;
  status: string;
}

export interface Inventario {
  id?: number;
  empresaId?: number;
  patrimonio: string;
  hostname: string;
  marca?: string;
  modelo?: string;
  tipo?: 'Computador' | 'Notebook' | 'Impressora' | 'Perifericos' | 'Outros';
  descricao?: string;
  dataCompra?: string;
  numeroNotaFiscal?: string;
  notaFiscalNome?: string;
  notaFiscalUrl?: string;
  responsavelUsuarioId?: number;
  responsavel?: string;
  unidadeEmpresaId?: number;
  unidade?: string;
  online?: boolean;
  usuario: string;
  filial: string;
  sistema: string;
  memoria: string;
  processador?: string;
  gpu?: string;
  memoriaLivreGb?: number;
  discoGb?: number;
  discoLivreGb?: number;
  discos?: DiscoInventario[];
  discosJson?: string;
  ip?: string;
  agentId?: string;
  agentVersion?: string;
  rustDeskId?: string;
  rustDeskPassword?: string;
}

export interface DiscoInventario {
  nome: string;
  unidade: string;
  totalGb: number;
  livreGb: number;
}

export interface LinkMonitorado {
  id?: number;
  empresaId?: number;
  nome: string;
  tipo: 'Link internet' | 'Firewall' | 'Site';
  local: 'Matriz' | 'Filial' | 'Site externo';
  firewall: string;
  endereco: string;
  cep: string;
  intervalo: number;
  pingMs: number;
  latitude: number;
  longitude: number;
  disponivel: boolean;
  chamado?: string;
}

export interface EmpresaLista {
  id?: number;
  nome?: string;
  razaoSocial: string;
  nomeFantasia: string;
  tenantSlug: string;
  cnpj: string;
  telefone: string;
  email: string;
  endereco: string;
  numero: string;
  complemento?: string;
  bairro?: string;
  cidade?: string;
  estado?: string;
  cep?: string;
  inscricaoMunicipal?: string;
  inscricaoEstadual?: string;
  logoUrl?: string;
  empresaContratanteId?: number | null;
  tipoUnidade?: 'Contratante' | 'Matriz' | 'Filial';
  ativo: boolean;
  acessoBloqueado: boolean;
  motivoBloqueio?: string;
  bloqueadoEm?: string | null;
  dataCadastro: string;
}

export type EmpresaForm = EmpresaLista & {
  nome: string;
};

export interface UsuarioLista {
  id?: number;
  empresaId?: number;
  empresaNome: string;
  nome: string;
  email: string;
  role: Perfil;
  ativo: boolean;
}

export interface LoginForm {
  email: string;
  senha: string;
}

export interface AdminSaasForm {
  nome: string;
  email: string;
  senha: string;
}

export interface NovoUsuarioForm {
  id?: number;
  empresaId: number | null;
  nome: string;
  email: string;
  senha: string;
  senhaConfirmacao?: string;
  role: Perfil;
  ativo?: boolean;
}

export interface CategoriaChamado {
  id?: number;
  empresaId?: number;
  nome: string;
  subcategorias: string[];
  prioridadePadrao: Prioridade;
  ativo: boolean;
}

export interface CategoriaChamadoForm {
  id?: number;
  empresaId?: number;
  nome: string;
  subcategoriasTexto: string;
  prioridadePadrao: Prioridade;
  ativo: boolean;
}

export interface DadosEmpresaSaas {
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  inscricaoMunicipal: string;
  inscricaoEstadual: string;
  regimeTributario: string;
  emailFinanceiro: string;
  telefone: string;
  endereco: string;
  numero: string;
  complemento: string;
  bairro: string;
  cidade: string;
  estado: string;
  cep: string;
}

export interface PlanoSaas {
  id: number;
  nome: string;
  descricao: string;
  limiteUsuarios: number;
  limiteEmpresas: number;
  valorMensal: number;
  ativo: boolean;
}

export interface AssinaturaSaas {
  id: number;
  empresaId: number;
  planoId: number;
  dataInicio: string;
  dataFim: string;
  status: 'Ativa' | 'Pausada' | 'Cancelada' | 'Teste';
  valorMensal: number;
}

export interface CobrancaSaas {
  id: number;
  empresaId: number;
  assinaturaId: number;
  vencimento: string;
  valor: number;
  status: 'Aberta' | 'Paga' | 'Vencida' | 'Cancelada';
  formaPagamento: string;
}

export interface FormaPagamentoSaas {
  id: number;
  nome: string;
  tipo: 'Mercado Pago' | 'Pix manual' | 'Boleto' | 'Cartao';
  chavePix: string;
  recebedorPix: string;
  cidadePix: string;
  mercadoPagoPublicKey: string;
  mercadoPagoAccessToken: string;
  ativo: boolean;
}

export interface DespesaSaas {
  id: number;
  descricao: string;
  fornecedor: string;
  categoria: string;
  vencimento: string;
  valor: number;
  status: 'Aberta' | 'Paga' | 'Atrasada';
}
