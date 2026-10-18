using System.Globalization;
using System.IO;

namespace JogoCorridaWinFormsApp
{
    /// <summary>Uma linha do ranking.</summary>
    internal class Colocacao
    {
        public string Nome { get; set; } = "";
        public int Pontos { get; set; }
        public int Ciclo { get; set; }
        public int Setor { get; set; }
        public DateTime Data { get; set; }
    }

    /// <summary>
    /// Ranking dos melhores jogadores, guardado em um arquivo de texto.
    ///
    /// O arquivo fica na RAIZ DO PROJETO (do lado do README.md), e nao em uma pasta
    /// escondida do Windows, de proposito: assim o ranking entra no git junto com o
    /// jogo e viaja com ele - no campeonato, basta dar commit no ranking.csv para as
    /// pontuacoes irem junto.
    ///
    /// A raiz e descoberta subindo as pastas a partir do executavel ate achar um
    /// arquivo de solucao (.sln/.slnx) ou a pasta .git. Se o jogo estiver solto, fora do
    /// projeto, o arquivo fica do lado do .exe. Se nem isso der (pasta sem permissao
    /// de escrita), cai para a pasta de dados do usuario.
    /// </summary>
    internal static class Ranking
    {
        public const int Tamanho = 10;
        public const int TamanhoMaximoDoNome = 12;

        private const string NomeDoArquivo = "ranking.csv";

        private static string? arquivo;
        private static List<Colocacao>? cache;

        /// <summary>Caminho completo do arquivo que esta sendo usado.</summary>
        public static string Arquivo => arquivo ??= DescobreArquivo();

        /// <summary>As colocacoes, da maior pontuacao para a menor.</summary>
        public static List<Colocacao> Colocacoes => cache ??= Carregar();

        public static int Recorde => Colocacoes.Count > 0 ? Colocacoes[0].Pontos : 0;

        /// <summary>A pontuacao entra no ranking?</summary>
        public static bool Qualifica(int pontos)
            => pontos > 0 && (Colocacoes.Count < Tamanho || pontos > Colocacoes[^1].Pontos);

        /// <summary>Guarda a corrida e devolve a posicao (1 = primeiro lugar; 0 = nao entrou).</summary>
        public static int Registrar(string nome, int pontos, int ciclo, int setor)
        {
            var nova = new Colocacao
            {
                Nome = LimpaNome(nome),
                Pontos = pontos,
                Ciclo = ciclo,
                Setor = setor,
                Data = DateTime.Now
            };

            var lista = Colocacoes;
            lista.Add(nova);
            lista.Sort((a, b) => b.Pontos.CompareTo(a.Pontos));
            if (lista.Count > Tamanho) lista.RemoveRange(Tamanho, lista.Count - Tamanho);

            Salvar(lista);

            var posicao = lista.IndexOf(nova);
            return posicao < 0 ? 0 : posicao + 1;
        }

        /// <summary>Tira o que atrapalha o arquivo e corta o tamanho.</summary>
        public static string LimpaNome(string nome)
        {
            nome = new string(nome.Where(c => !char.IsControl(c) && c != ';').ToArray()).Trim();
            if (nome.Length > TamanhoMaximoDoNome) nome = nome[..TamanhoMaximoDoNome];
            return nome.Length == 0 ? "SELENE" : nome.ToUpperInvariant();
        }

        // =====================================================================
        // ONDE FICA O ARQUIVO
        // =====================================================================

        private static string DescobreArquivo() => DescobreArquivo(AppContext.BaseDirectory);

        /// <summary>
        /// Decide onde fica o ranking, a partir da pasta do executavel.
        /// Separado assim para dar para testar sem abrir o jogo.
        /// </summary>
        public static string DescobreArquivo(string pastaDoExecutavel)
        {
            var raiz = RaizDoProjeto(pastaDoExecutavel);
            if (raiz is not null && PodeEscrever(raiz)) return Path.Combine(raiz, NomeDoArquivo);

            if (PodeEscrever(pastaDoExecutavel)) return Path.Combine(pastaDoExecutavel, NomeDoArquivo);

            // ultimo recurso: pasta de dados do usuario
            var dados = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JogoCorrida");
            Directory.CreateDirectory(dados);
            return Path.Combine(dados, NomeDoArquivo);
        }

        /// <summary>Sobe as pastas ate achar a solucao ou o repositorio git.</summary>
        private static string? RaizDoProjeto(string pastaDoExecutavel)
        {
            try
            {
                var pasta = new DirectoryInfo(pastaDoExecutavel);
                for (int i = 0; i < 8 && pasta is not null; i++)
                {
                    var temSolucao = pasta.EnumerateFiles("*.sln*").Any();
                    var temGit = Directory.Exists(Path.Combine(pasta.FullName, ".git"));
                    if (temSolucao || temGit) return pasta.FullName;
                    pasta = pasta.Parent;
                }
            }
            catch
            {
                // sem acesso ao disco: usa o proximo caminho da lista
            }
            return null;
        }

        private static bool PodeEscrever(string pasta)
        {
            try
            {
                var teste = Path.Combine(pasta, ".escrita-" + Guid.NewGuid().ToString("N")[..8]);
                File.WriteAllText(teste, "");
                File.Delete(teste);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // =====================================================================
        // LEITURA E ESCRITA
        // =====================================================================

        private static List<Colocacao> Carregar()
        {
            var lista = new List<Colocacao>();

            try
            {
                if (!File.Exists(Arquivo)) return lista;

                foreach (var linha in File.ReadAllLines(Arquivo))
                {
                    if (string.IsNullOrWhiteSpace(linha)) continue;
                    if (linha.TrimStart().StartsWith('#')) continue;   // comentario

                    var campos = linha.Split(';');
                    if (campos.Length < 2) continue;
                    if (!int.TryParse(campos[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pontos)) continue;

                    lista.Add(new Colocacao
                    {
                        Nome = LimpaNome(campos[0]),
                        Pontos = pontos,
                        Ciclo = campos.Length > 2 && int.TryParse(campos[2], out var c) ? c : 1,
                        Setor = campos.Length > 3 && int.TryParse(campos[3], out var s) ? s : 1,
                        Data = campos.Length > 4 && DateTime.TryParse(campos[4], CultureInfo.InvariantCulture,
                                                                     DateTimeStyles.None, out var d)
                               ? d : DateTime.MinValue
                    });
                }
            }
            catch
            {
                // arquivo corrompido ou sem permissao: comeca um ranking novo
                return [];
            }

            lista.Sort((a, b) => b.Pontos.CompareTo(a.Pontos));
            if (lista.Count > Tamanho) lista.RemoveRange(Tamanho, lista.Count - Tamanho);
            return lista;
        }

        private static void Salvar(List<Colocacao> lista)
        {
            var linhas = new List<string>
            {
                "# POST HUMAN RUN - as 10 melhores corridas",
                "# nome;pontos;ciclo;setor;data"
            };

            linhas.AddRange(lista.Select(c =>
                $"{c.Nome};{c.Pontos.ToString(CultureInfo.InvariantCulture)};{c.Ciclo};{c.Setor};" +
                c.Data.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));

            try
            {
                // grava primeiro em um arquivo temporario: se faltar energia no meio
                // da escrita, o ranking que ja estava salvo nao se perde
                var temporario = Arquivo + ".tmp";
                File.WriteAllLines(temporario, linhas);

                if (File.Exists(Arquivo)) File.Replace(temporario, Arquivo, null);
                else File.Move(temporario, Arquivo);
            }
            catch
            {
                try
                {
                    File.WriteAllLines(Arquivo, linhas);
                }
                catch
                {
                    // se nao der para gravar, o jogo continua normalmente
                }
            }
        }
    }
}
