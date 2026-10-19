using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjetoAtendimento
{
    public enum StatusSenha
    {
        Aguardando,
        Chamada,
        EmAtendimento,
        Concluida
    }

    public class Senha
    {
        public int Numero { get; }
        public bool Prioritaria { get; }
        public DateTime EmitidaEm { get; }
        public StatusSenha Status { get; set; }

        public Senha(int numero, bool prioritaria)
        {
            Numero = numero;
            Prioritaria = prioritaria;
            EmitidaEm = DateTime.Now;
            Status = StatusSenha.Aguardando;
        }

        public override string ToString()
        {
            string tipo = Prioritaria ? "PRIORITÁRIA" : "COMUM";
            return $"Senha {Numero:000} - {tipo} - {Status} (emitida às {EmitidaEm:HH:mm:ss})";
        }
    }

    public class Atendimento
    {
        public Senha Senha { get; }
        public int NumeroGuiche { get; }
        public string Atendente { get; }
        public DateTime Inicio { get; }
        public DateTime? Fim { get; private set; }
        public bool Concluido => Fim.HasValue;
        public TimeSpan Duracao => (Fim ?? DateTime.Now) - Inicio;

        public Atendimento(Senha senha, int numeroGuiche, string atendente)
        {
            Senha = senha;
            NumeroGuiche = numeroGuiche;
            Atendente = atendente;
            Inicio = DateTime.Now;
        }

        public void Finalizar()
        {
            if (!Fim.HasValue)
                Fim = DateTime.Now;
        }

        public override string ToString()
        {
            string fim = Fim.HasValue ? Fim.Value.ToString("dd/MM/yyyy HH:mm:ss") : "Em andamento";
            string duracao = Concluido ? Duracao.ToString(@"hh\:mm\:ss") : "em andamento";
            return $"Senha {Senha.Numero:000} | Guichê {NumeroGuiche} | Atendente: {Atendente} | Início: {Inicio:dd/MM/yyyy HH:mm:ss} | Fim: {fim} | Duração: {duracao}";
        }
    }

    public class Guiche
    {
        public int Numero { get; }
        public Senha? SenhaChamada { get; private set; }
        public Atendimento? AtendimentoAtual { get; private set; }
        public List<Atendimento> Historico { get; } = new();

        public bool Ocupado => AtendimentoAtual != null;

        public Guiche(int numero)
        {
            Numero = numero;
        }

        public bool ChamarSenha(Senha senha)
        {
            if (Ocupado || SenhaChamada != null)
                return false;

            SenhaChamada = senha;
            senha.Status = StatusSenha.Chamada;
            return true;
        }

        public bool IniciarAtendimento(string atendente)
        {
            if (SenhaChamada == null || Ocupado || string.IsNullOrWhiteSpace(atendente))
                return false;

            AtendimentoAtual = new Atendimento(SenhaChamada, Numero, atendente.Trim());
            SenhaChamada.Status = StatusSenha.EmAtendimento;
            SenhaChamada = null;
            Historico.Add(AtendimentoAtual);
            return true;
        }

        public bool FinalizarAtendimento()
        {
            if (AtendimentoAtual == null)
                return false;

            AtendimentoAtual.Finalizar();
            AtendimentoAtual.Senha.Status = StatusSenha.Concluida;
            AtendimentoAtual = null;
            return true;
        }
    }

    public class SistemaAtendimento
    {
        private readonly Queue<Senha> filaPrioritaria = new();
        private readonly Queue<Senha> filaComum = new();
        private readonly List<Guiche> guiches = new();
        private readonly List<Senha> senhasEmitidas = new();
        private int proximaSenha = 1;

        public IReadOnlyList<Guiche> Guiches => guiches.AsReadOnly();
        public IReadOnlyList<Senha> SenhasEmitidas => senhasEmitidas.AsReadOnly();
        public int Aguardando => filaPrioritaria.Count + filaComum.Count;

        public Senha EmitirSenha(bool prioritaria)
        {
            var senha = new Senha(proximaSenha++, prioritaria);
            senhasEmitidas.Add(senha);
            if (prioritaria)
                filaPrioritaria.Enqueue(senha);
            else
                filaComum.Enqueue(senha);
            return senha;
        }

        public Guiche? CadastrarGuiche()
        {
            int numero = guiches.Count == 0 ? 1 : guiches.Max(g => g.Numero) + 1;
            var guiche = new Guiche(numero);
            guiches.Add(guiche);
            return guiche;
        }

        public Guiche? BuscarGuiche(int numero) =>
            guiches.FirstOrDefault(g => g.Numero == numero);

        public Senha? ChamarProximaSenha(int numeroGuiche)
        {
            var guiche = BuscarGuiche(numeroGuiche);
            if (guiche == null || guiche.Ocupado || guiche.SenhaChamada != null || Aguardando == 0)
                return null;

            // Senhas prioritárias são chamadas antes das comuns.
            Senha senha = filaPrioritaria.Count > 0
                ? filaPrioritaria.Dequeue()
                : filaComum.Dequeue();

            return guiche.ChamarSenha(senha) ? senha : null;
        }

        public IReadOnlyList<Atendimento> HistoricoDoGuiche(int numeroGuiche)
        {
            var guiche = BuscarGuiche(numeroGuiche);
            return guiche == null ? Array.Empty<Atendimento>() : guiche.Historico.AsReadOnly();
        }

        public IEnumerable<Atendimento> TodosAtendimentos() =>
            guiches.SelectMany(g => g.Historico);

        public void ExibirFila()
        {
            Console.WriteLine("\n--- FILA DE ESPERA ---");
            Console.WriteLine($"Prioritárias: {filaPrioritaria.Count} | Comuns: {filaComum.Count} | Total: {Aguardando}");
            if (filaPrioritaria.Count > 0)
                Console.WriteLine("Próximas prioritárias: " + string.Join(", ", filaPrioritaria.Select(s => s.Numero.ToString("000"))));
            if (filaComum.Count > 0)
                Console.WriteLine("Próximas comuns: " + string.Join(", ", filaComum.Select(s => s.Numero.ToString("000"))));
        }
    }

    internal static class Program
    {
        private static readonly SistemaAtendimento Sistema = new();

        private static string LerTexto(string mensagem)
        {
            Console.Write(mensagem);
            return Console.ReadLine()?.Trim() ?? "";
        }

        private static int LerInteiro(string mensagem)
        {
            while (true)
            {
                Console.Write(mensagem);
                if (int.TryParse(Console.ReadLine(), out int valor))
                    return valor;
                Console.WriteLine("Digite um número válido.");
            }
        }

        private static void EmitirSenha()
        {
            string resposta = LerTexto("Senha prioritária? (s/n): ").ToLowerInvariant();
            bool prioritaria = resposta == "s" || resposta == "sim";
            Senha senha = Sistema.EmitirSenha(prioritaria);
            Console.WriteLine("Senha emitida: " + senha);
            Sistema.ExibirFila();
        }

        private static void CadastrarGuiche()
        {
            Guiche guiche = Sistema.CadastrarGuiche()!;
            Console.WriteLine($"Guichê {guiche.Numero} cadastrado.");
        }

        private static void ChamarProxima()
        {
            if (Sistema.Guiches.Count == 0)
            {
                Console.WriteLine("Cadastre um guichê primeiro.");
                return;
            }

            ListarGuiches();
            int numero = LerInteiro("Número do guichê: ");
            Guiche? guiche = Sistema.BuscarGuiche(numero);
            if (guiche == null)
            {
                Console.WriteLine("Guichê não encontrado.");
                return;
            }
            if (guiche.Ocupado)
            {
                Console.WriteLine("Esse guichê está realizando um atendimento. Finalize-o antes de chamar outra senha.");
                return;
            }
            if (guiche.SenhaChamada != null)
            {
                Console.WriteLine($"A senha {guiche.SenhaChamada.Numero:000} já foi chamada. Inicie o atendimento ou finalize/cancele essa chamada antes de chamar outra.");
                return;
            }

            Senha? senha = Sistema.ChamarProximaSenha(numero);
            Console.WriteLine(senha == null
                ? "Não há senhas aguardando na fila."
                : $"Guichê {numero}: senha chamada — {senha.Numero:000} ({(senha.Prioritaria ? "prioritária" : "comum")}).");
            Sistema.ExibirFila();
        }

        private static void IniciarAtendimento()
        {
            ListarGuiches();
            int numero = LerInteiro("Número do guichê: ");
            Guiche? guiche = Sistema.BuscarGuiche(numero);
            if (guiche == null)
            {
                Console.WriteLine("Guichê não encontrado.");
                return;
            }
            if (guiche.SenhaChamada == null)
            {
                Console.WriteLine("Nenhuma senha foi chamada para esse guichê.");
                return;
            }

            string atendente = LerTexto("Nome do atendente: ");
            if (!guiche.IniciarAtendimento(atendente))
            {
                Console.WriteLine("Não foi possível iniciar o atendimento. Confira o nome do atendente e o estado do guichê.");
                return;
            }

            Console.WriteLine($"Atendimento iniciado: senha {guiche.AtendimentoAtual!.Senha.Numero:000}, guichê {numero}, atendente {atendente}.");
        }

        private static void FinalizarAtendimento()
        {
            ListarGuiches();
            int numero = LerInteiro("Número do guichê: ");
            Guiche? guiche = Sistema.BuscarGuiche(numero);
            if (guiche == null)
            {
                Console.WriteLine("Guichê não encontrado.");
                return;
            }
            Atendimento? atendimento = guiche.AtendimentoAtual;
            if (atendimento == null)
            {
                Console.WriteLine("Esse guichê não possui atendimento em andamento.");
                return;
            }

            guiche.FinalizarAtendimento();
            Console.WriteLine("Atendimento finalizado:");
            Console.WriteLine(atendimento);
        }

        private static void ListarAtendimentosPorGuiche()
        {
            if (Sistema.Guiches.Count == 0)
            {
                Console.WriteLine("Nenhum guichê cadastrado.");
                return;
            }

            ListarGuiches();
            int numero = LerInteiro("Número do guichê para consultar (0 = todos): ");
            IEnumerable<Atendimento> lista = numero == 0
                ? Sistema.TodosAtendimentos()
                : Sistema.HistoricoDoGuiche(numero);

            var atendimentos = lista.ToList();
            if (atendimentos.Count == 0)
            {
                Console.WriteLine("Não há atendimentos registrados para essa seleção.");
                return;
            }

            Console.WriteLine("\n--- HISTÓRICO DE ATENDIMENTOS ---");
            foreach (Atendimento atendimento in atendimentos.OrderBy(a => a.Inicio))
                Console.WriteLine(atendimento);

            Console.WriteLine($"\nTotal de atendimentos iniciados: {atendimentos.Count}");
            Console.WriteLine($"Concluídos: {atendimentos.Count(a => a.Concluido)}");
            Console.WriteLine($"Em andamento: {atendimentos.Count(a => !a.Concluido)}");
        }

        private static void ListarGuiches()
        {
            Console.WriteLine("\n--- GUICHÊS ---");
            if (Sistema.Guiches.Count == 0)
            {
                Console.WriteLine("Nenhum guichê cadastrado.");
                return;
            }
            foreach (Guiche g in Sistema.Guiches)
            {
                string estado = g.Ocupado
                    ? $"Em atendimento (senha {g.AtendimentoAtual!.Senha.Numero:000})"
                    : g.SenhaChamada != null
                        ? $"Senha {g.SenhaChamada.Numero:000} chamada, aguardando início"
                        : "Disponível";
                Console.WriteLine($"Guichê {g.Numero}: {estado}");
            }
        }

        private static void Menu()
        {
            Console.WriteLine("\n========== SISTEMA DE ATENDIMENTO ==========");
            Console.WriteLine("0. Sair");
            Console.WriteLine("1. Emitir nova senha (informar se é prioritária)");
            Console.WriteLine("2. Cadastrar novo guichê");
            Console.WriteLine("3. Chamar a próxima senha para um guichê específico");
            Console.WriteLine("4. Iniciar atendimento (informar atendente)");
            Console.WriteLine("5. Finalizar atendimento");
            Console.WriteLine("6. Listar atendimentos realizados por guichê");
            Console.WriteLine("7. Consultar estado dos guichês");
            Console.WriteLine("8. Exibir fila de espera");
        }

        private static void Main()
        {
            bool sair = false;
            while (!sair)
            {
                Menu();
                int opcao = LerInteiro("Escolha uma opção: ");
                switch (opcao)
                {
                    case 0:
                        sair = true;
                        break;
                    case 1:
                        EmitirSenha();
                        break;
                    case 2:
                        CadastrarGuiche();
                        break;
                    case 3:
                        ChamarProxima();
                        break;
                    case 4:
                        IniciarAtendimento();
                        break;
                    case 5:
                        FinalizarAtendimento();
                        break;
                    case 6:
                        ListarAtendimentosPorGuiche();
                        break;
                    case 7:
                        ListarGuiches();
                        break;
                    case 8:
                        Sistema.ExibirFila();
                        break;
                    default:
                        Console.WriteLine("Opção inválida.");
                        break;
                }
            }
            Console.WriteLine("\nPrograma encerrado.");
        }
    }
}
