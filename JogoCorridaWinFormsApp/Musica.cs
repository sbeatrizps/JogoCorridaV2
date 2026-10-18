using System.IO;
namespace JogoCorridaWinFormsApp
{
    /// <summary>
    /// Trilha sonora de fundo (arquivos MP3 livres, em Assets/Audio - ver CREDITOS.txt).
    ///
    /// Usa o System.Windows.Media.MediaPlayer porque o caminho antigo do Windows para
    /// tocar MP3 (MCI / mciSendString) nao existe mais no Windows 11. O MediaPlayer
    /// toca em segundo plano, tem controle de volume e nao trava o jogo.
    /// </summary>
    internal static class Musica
    {
        private static System.Windows.Media.MediaPlayer? tocador;
        private static string faixaAtual = "";
        private static bool reiniciando;

        public static bool Ligada { get; private set; } = true;
        public static double Volume { get; set; } = 0.30;

        private static string Pasta => Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");

        /// <summary>Troca a faixa (nao faz nada se ja for a que esta tocando).</summary>
        public static void Tocar(string arquivo)
        {
            if (string.IsNullOrWhiteSpace(arquivo) || arquivo == faixaAtual) return;

            var caminho = Path.Combine(Pasta, arquivo);
            if (!File.Exists(caminho)) return;

            try
            {
                tocador ??= new System.Windows.Media.MediaPlayer();
                tocador.Open(new Uri(caminho));
                tocador.Volume = Ligada ? Volume : 0;
                tocador.Play();
                faixaAtual = arquivo;
            }
            catch
            {
                // sem audio disponivel: o jogo continua em silencio
                tocador = null;
            }
        }

        /// <summary>
        /// Chamada a cada quadro: quando a musica chega ao fim, volta para o comeco.
        /// (O MediaPlayer nao tem repeticao automatica.)
        /// </summary>
        public static void Atualizar()
        {
            if (tocador is null || !Ligada) return;

            try
            {
                if (!tocador.NaturalDuration.HasTimeSpan) return;

                var duracao = tocador.NaturalDuration.TimeSpan;
                var faltando = duracao - tocador.Position;

                if (faltando <= TimeSpan.FromMilliseconds(120))
                {
                    if (reiniciando) return;
                    reiniciando = true;
                    tocador.Position = TimeSpan.Zero;
                    tocador.Play();
                }
                else
                {
                    reiniciando = false;
                }
            }
            catch
            {
                // ignora falhas pontuais do player
            }
        }

        public static void Pausar()
        {
            try { tocador?.Pause(); } catch { }
        }

        public static void Retomar()
        {
            if (!Ligada) return;
            try { tocador?.Play(); } catch { }
        }

        /// <summary>Liga/desliga a trilha (a tecla M controla trilha e efeitos juntos).</summary>
        public static void Alternar(bool ligada)
        {
            Ligada = ligada;
            try
            {
                if (tocador is null) return;
                tocador.Volume = ligada ? Volume : 0;
                if (ligada) tocador.Play(); else tocador.Pause();
            }
            catch { }
        }

        public static void Parar()
        {
            try
            {
                tocador?.Stop();
                tocador?.Close();
            }
            catch { }
            finally
            {
                tocador = null;
                faixaAtual = "";
            }
        }
    }
}
