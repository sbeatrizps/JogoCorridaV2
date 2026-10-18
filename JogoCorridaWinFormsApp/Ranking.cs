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
    /// Ranking dos melhores jogadores, guardado em um arquivo de texto simples
    /// (um jogador por linha, campos separados por ponto e virgula).
    ///
    /// O arquivo fica em:  %APPDATA%\JogoCorrida\ranking.csv
    /// Da para abrir no Bloco de Notas ou no Excel.
    /// </summary>
    internal static class Ranking
    {
        public const int Tamanho = 10;
        public const int TamanhoMaximoDoNome = 12;

        private static readonly string Pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JogoCorrida");

        public static string Arquivo => Path.Combine(Pasta, "ranking.csv");

        /// <summary>Arquivo com o ultimo nome digitado, so para nao ter que redigitar.</summary>
        private static string ArquivoUltimoNome => Path.Combine(Pasta, "ultimo-nome.txt");

        private static List<Colocacao>? cache;

        /// <summary>As colocacoes, da maior pontuacao para a menor.</summary>
        public static List<Colocacao> Colocacoes
        {
            get
            {
                cache ??= Carregar();
                return cache;
            }
        }

        public static int Recorde => Colocacoes.Count > 0 ? Colocacoes[0].Pontos : 0;

        /// <summary>A pontuacao entra no ranking?</summary>
        public static bool Qualifica(int pontos)
            => pontos > 0 && (Colocacoes.Count < Tamanho || pontos > Colocacoes[^1].Pontos);

        /// <summary>Guarda a corrida e devolve a posicao (1 = primeiro lugar; 0 = nao entrou).</summary>
        public static int Registrar(string nome, int pontos, int ciclo, int setor)
        {
            nome = LimpaNome(nome);

            var nova = new Colocacao
            {
                Nome = nome,
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
            SalvarUltimoNome(nome);

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

        public static string UltimoNome()
        {
            try
            {
                if (File.Exists(ArquivoUltimoNome))
                {
                    return LimpaNome(File.ReadAllText(ArquivoUltimoNome));
                }
            }
            catch
            {
                // sem problema: comeca com o nome vazio
            }
            return "";
        }

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
            try
            {
                Directory.CreateDirectory(Pasta);

                var linhas = lista.Select(c =>
                    $"{c.Nome};{c.Pontos.ToString(CultureInfo.InvariantCulture)};{c.Ciclo};{c.Setor};" +
                    c.Data.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

                File.WriteAllLines(Arquivo, linhas);
            }
            catch
            {
                // se nao der para gravar, o jogo continua normalmente
            }
        }

        private static void SalvarUltimoNome(string nome)
        {
            try
            {
                Directory.CreateDirectory(Pasta);
                File.WriteAllText(ArquivoUltimoNome, nome);
            }
            catch
            {
                // idem
            }
        }
    }
}
