using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using JogoCorrida;

/// <summary>
/// Versao em modo texto do jogo. Usa o MESMO motor (classe Jogo) da versao WinForms:
/// as mesmas fases, os mesmos power-ups, os mesmos chefes e o mesmo espacamento
/// seguro de obstaculos. So muda a forma de desenhar e de tocar os sons.
/// </summary>
[SupportedOSPlatform("windows")]
class Program
{
    private const int Colunas = 47;
    private const int Linhas = 26;

    private static readonly char[,] tela = new char[Linhas, Colunas];
    private static readonly ConsoleColor[,] cores = new ConsoleColor[Linhas, Colunas];

    private static readonly Jogo jogo = new();
    private static DateTime proximoSom = DateTime.MinValue;

    static void Main()
    {
        PreparaConsole();
        ConfiguraJogo();

        jogo.Som += TocarSom;
        jogo.IniciaJogo();

        var relogio = Stopwatch.StartNew();
        var ultimoQuadro = relogio.Elapsed.TotalMilliseconds;
        var rodando = true;

        while (rodando)
        {
            var agora = relogio.Elapsed.TotalMilliseconds;
            var delta = agora - ultimoQuadro;
            ultimoQuadro = agora;

            rodando = LeTeclado();
            jogo.Atualizar(delta);

            DesenhaCenario();
            DesenhaElementos();
            DesenhaChefe();
            DesenhaJogador();
            DesenhaMensagens();
            MostraTela();
            DesenhaPainel();

            Thread.Sleep(16);
        }

        Console.CursorVisible = true;
        Console.ResetColor();
        Console.SetCursorPosition(0, Linhas + 4);
    }

    // =========================================================================
    // PREPARACAO
    // =========================================================================

    private static void PreparaConsole()
    {
        Console.CursorVisible = false;
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "JOGO CORRIDA :: p0ST hUMAN rUN";

        try
        {
            Console.SetWindowSize(Colunas + 2, Linhas + 6);
            Console.SetBufferSize(Colunas + 2, Linhas + 6);
        }
        catch
        {
            // alguns terminais nao deixam redimensionar - segue o jogo
        }

        Console.Clear();
    }

    private static void ConfiguraJogo()
    {
        var cfg = jogo.Config;
        cfg.Largura = Colunas;
        cfg.Altura = Linhas;
        cfg.QtdFaixas = 3;
        cfg.MargemPista = 4;
        cfg.LarguraJogador = 5;
        cfg.AlturaJogador = 3;
        cfg.LarguraLagrima = cfg.AlturaLagrima = 1;
        cfg.LarguraPowerUp = cfg.AlturaPowerUp = 2;
        cfg.RecuoJogador = 2;
        cfg.VelocidadeLateral = 26;
        cfg.FolgaColisao = 0.6;
        cfg.DuracaoExplosaoMs = 700;
    }

    // =========================================================================
    // ENTRADA
    // =========================================================================

    /// <summary>Le o teclado. Devolve false quando o jogador pede para sair.</summary>
    private static bool LeTeclado()
    {
        while (Console.KeyAvailable)
        {
            var tecla = Console.ReadKey(true);
            switch (tecla.Key)
            {
                case ConsoleKey.LeftArrow:
                case ConsoleKey.A:
                    jogo.MoverEsquerda();
                    break;

                case ConsoleKey.RightArrow:
                case ConsoleKey.D:
                    jogo.MoverDireita();
                    break;

                case ConsoleKey.Enter:
                case ConsoleKey.Spacebar:
                    jogo.Confirmar();
                    break;

                case ConsoleKey.P:
                    jogo.AlternarPausa();
                    break;

                case ConsoleKey.Escape:
                    if (jogo.Estado == EstadoJogo.Jogando) jogo.AlternarPausa();
                    else return false;
                    break;
            }
        }
        return true;
    }

    // =========================================================================
    // DESENHO
    // =========================================================================

    private static void DesenhaCenario()
    {
        var cfg = jogo.Config;

        for (int l = 0; l < Linhas; l++)
        {
            for (int c = 0; c < Colunas; c++)
            {
                var naPista = c >= cfg.MargemPista && c < cfg.MargemPista + cfg.LarguraPista;
                tela[l, c] = naPista ? ' ' : (l % 3 == 0 ? '-' : ' ');
                cores[l, c] = naPista ? ConsoleColor.Gray : ConsoleColor.DarkMagenta;
            }
        }

        for (int l = 0; l < Linhas; l++)
        {
            Escreve(l, cfg.MargemPista, '|', ConsoleColor.Magenta);
            Escreve(l, cfg.MargemPista + cfg.LarguraPista - 1, '|', ConsoleColor.Magenta);
        }

        var deslocamento = (int)(jogo.DistanciaPercorrida % 4);
        for (int i = 1; i < cfg.QtdFaixas; i++)
        {
            var coluna = cfg.MargemPista + cfg.LarguraFaixa * i;
            for (int l = -deslocamento; l < Linhas; l += 4)
            {
                Escreve(l, coluna, ':', ConsoleColor.DarkGray);
                Escreve(l + 1, coluna, ':', ConsoleColor.DarkGray);
            }
        }
    }

    private static void DesenhaElementos()
    {
        foreach (var el in jogo.Elementos)
        {
            var (simbolo, cor) = el.Tipo switch
            {
                TipoElemento.Lagrima => ('o', ConsoleColor.Cyan),
                TipoElemento.PowerUp => ('*', ConsoleColor.Green),
                _ => el.Obstaculo switch
                {
                    TipoObstaculo.Vulto => ('A', ConsoleColor.Magenta),
                    TipoObstaculo.Executor => ('H', ConsoleColor.Red),
                    TipoObstaculo.Barreira => ('^', ConsoleColor.DarkMagenta),
                    TipoObstaculo.Miragem => ('~', ConsoleColor.DarkCyan),
                    TipoObstaculo.Drone => ('X', ConsoleColor.White),
                    _ => ('?', ConsoleColor.White)
                }
            };

            Preenche(el, simbolo, cor);
        }
    }

    private static void DesenhaChefe()
    {
        if (jogo.Chefe is null) return;
        Preenche(jogo.Chefe, '#', ConsoleColor.Red);
    }

    private static void DesenhaJogador()
    {
        // pisca enquanto esta invencivel
        if (jogo.Invencivel && (int)(jogo.TempoInvencivelMs / 120) % 2 == 0) return;

        var explodindo = jogo.Estado == EstadoJogo.Colidindo;
        Preenche(jogo.Jogador, explodindo ? '*' : (jogo.TemEscudo ? '0' : '8'),
                 explodindo ? ConsoleColor.Yellow : ConsoleColor.Cyan);

        if (explodindo)
        {
            var raio = (int)((1 - jogo.IntensidadeExplosao) * 6) + 1;
            var centroL = (int)jogo.Jogador.CentroY;
            var centroC = (int)jogo.Jogador.CentroX;

            for (int a = 0; a < 12; a++)
            {
                var angulo = a * Math.PI / 6;
                Escreve(centroL + (int)(Math.Sin(angulo) * raio),
                        centroC + (int)(Math.Cos(angulo) * raio * 1.8),
                        a % 2 == 0 ? '*' : '+',
                        a % 3 == 0 ? ConsoleColor.Red : ConsoleColor.Yellow);
            }
        }
    }

    private static void DesenhaMensagens()
    {
        switch (jogo.Estado)
        {
            case EstadoJogo.Menu:
                Centraliza(Linhas / 2 - 5, "== JOGO CORRIDA ==", ConsoleColor.White);
                Centraliza(Linhas / 2 - 4, "voce e SELENE (NYX-01)", ConsoleColor.Magenta);
                Centraliza(Linhas / 2 - 2, "SETAS: trocar de faixa", ConsoleColor.Gray);
                Centraliza(Linhas / 2 - 1, "P: pausar   ESC: sair", ConsoleColor.Gray);
                Centraliza(Linhas / 2 + 1, "o lagrima   * Insight  = PEGUE", ConsoleColor.Cyan);
                Centraliza(Linhas / 2 + 2, "A H ^ X     = FUJA", ConsoleColor.Magenta);
                Centraliza(Linhas / 2 + 4, "ENTER para comecar", ConsoleColor.Green);
                break;

            case EstadoJogo.Dialogo:
                var fala = jogo.FalaAtual;
                if (fala is not null)
                {
                    Centraliza(Linhas / 2 - 2, fala.Personagem, ConsoleColor.Cyan);
                    var texto = fala.Texto;
                    var limite = Colunas - 6;
                    var linha = 0;
                    while (texto.Length > 0 && linha < 4)
                    {
                        var corte = texto.Length <= limite ? texto.Length : texto.LastIndexOf(' ', limite);
                        if (corte <= 0) corte = Math.Min(limite, texto.Length);
                        Centraliza(Linhas / 2 + linha, texto[..corte].Trim(), ConsoleColor.White);
                        texto = texto[corte..].Trim();
                        linha++;
                    }
                    Centraliza(Linhas / 2 + 5, "ENTER para continuar", ConsoleColor.DarkGray);
                }
                break;

            case EstadoJogo.Pausado:
                Centraliza(Linhas / 2, ">>> PAUSA <<<", ConsoleColor.Yellow);
                Centraliza(Linhas / 2 + 2, "ENTER para continuar", ConsoleColor.Gray);
                break;

            case EstadoJogo.TrocandoSetor:
                Centraliza(Linhas / 2 - 1, $"FASE {jogo.NumeroSetor}", ConsoleColor.Cyan);
                Centraliza(Linhas / 2, jogo.FaseAtual.Nome, ConsoleColor.White);
                Centraliza(Linhas / 2 + 2, "+1 vida de bonus", ConsoleColor.Magenta);
                break;

            case EstadoJogo.GameOver:
                Centraliza(Linhas / 2 - 5, "*** GAME OVER ***", ConsoleColor.Red);
                Centraliza(Linhas / 2 - 4, $"ciclo {jogo.Ciclo}, setor {jogo.NumeroSetor}", ConsoleColor.Magenta);
                Centraliza(Linhas / 2 - 3, $"Pontuacao: {jogo.Pontuacao}", ConsoleColor.White);
                Centraliza(Linhas / 2 - 2, $"tempo {(int)jogo.PontosTempo}", ConsoleColor.Gray);
                Centraliza(Linhas / 2 - 1, $"obstaculos {jogo.ObstaculosSuperados} ({(int)jogo.PontosObstaculos})", ConsoleColor.Gray);
                Centraliza(Linhas / 2, $"lagrimas {jogo.Lagrimas} ({(int)jogo.PontosLagrimas})", ConsoleColor.Gray);
                Centraliza(Linhas / 2 + 1, $"power-ups {jogo.PowerUpsPegos}", ConsoleColor.Gray);
                Centraliza(Linhas / 2 + 2, $"Recorde: {jogo.MelhorPontuacao}", ConsoleColor.Yellow);
                Centraliza(Linhas / 2 + 4, "ENTER joga de novo | ESC sai", ConsoleColor.Green);
                break;
        }
    }

    /// <summary>Escreve o quadro inteiro de uma vez, para nao piscar.</summary>
    private static void MostraTela()
    {
        Console.SetCursorPosition(0, 0);

        var linha = new StringBuilder(Colunas);
        for (int l = 0; l < Linhas; l++)
        {
            var corAtual = cores[l, 0];
            linha.Clear();

            for (int c = 0; c < Colunas; c++)
            {
                if (cores[l, c] != corAtual)
                {
                    Console.ForegroundColor = corAtual;
                    Console.Write(linha.ToString());
                    linha.Clear();
                    corAtual = cores[l, c];
                }
                linha.Append(tela[l, c]);
            }

            Console.ForegroundColor = corAtual;
            Console.WriteLine(linha.ToString());
        }
    }

    private static void DesenhaPainel()
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($" PONTOS {jogo.Pontuacao,-7} VIDAS {new string('<', Math.Max(0, jogo.Vidas)),-5} " +
                          $"<3 {jogo.Lagrimas,-3}{(jogo.Multiplicador > 1 ? " x2" : "")}".PadRight(Colunas));

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($" SETOR {jogo.NumeroSetor}/3  CICLO {jogo.Ciclo}  {jogo.FaseAtual.Nome}" +
                          $"{(jogo.ControlesInvertidos ? "  [CONTROLES INVERTIDOS]" : "")}".PadRight(Colunas));

        var largura = 22;
        var fracao = jogo.ChefeAtivo ? jogo.EnergiaChefe / 100.0 : jogo.ProgressoSetor;
        var cheio = (int)(largura * Math.Clamp(fracao, 0, 1));

        Console.ForegroundColor = jogo.ChefeAtivo ? ConsoleColor.Red : ConsoleColor.Cyan;
        var rotulo = jogo.ChefeAtivo ? jogo.FaseAtual.NomeChefe : "progresso";
        Console.WriteLine($" [{new string('=', cheio)}{new string(' ', largura - cheio)}] {rotulo}".PadRight(Colunas));
        Console.ResetColor();
    }

    // =========================================================================
    // AUXILIARES DE DESENHO
    // =========================================================================

    private static void Preenche(Elemento el, char simbolo, ConsoleColor cor)
    {
        for (int l = 0; l < el.Altura; l++)
        {
            for (int c = 0; c < el.Largura; c++)
            {
                Escreve((int)Math.Round(el.PosicaoY) + l, (int)Math.Round(el.PosicaoX) + c, simbolo, cor);
            }
        }
    }

    private static void Escreve(int linha, int coluna, char simbolo, ConsoleColor cor)
    {
        if (linha < 0 || linha >= Linhas || coluna < 0 || coluna >= Colunas) return;
        tela[linha, coluna] = simbolo;
        cores[linha, coluna] = cor;
    }

    private static void Centraliza(int linha, string texto, ConsoleColor cor)
    {
        var inicio = (Colunas - texto.Length) / 2;
        for (int i = 0; i < texto.Length; i++)
        {
            Escreve(linha, inicio + i, texto[i], cor);
        }
    }

    // =========================================================================
    // SOM
    // =========================================================================

    /// <summary>
    /// No console os efeitos saem pelo alto-falante (Console.Beep). O apito roda
    /// em outra thread para nao travar o jogo, com um intervalo minimo entre um e outro.
    /// </summary>
    private static void TocarSom(EventoSom evento)
    {
        if (DateTime.Now < proximoSom) return;

        var notas = evento switch
        {
            EventoSom.Lagrima => new[] { (988, 60), (1319, 70) },
            EventoSom.PegouPowerUp => [(523, 60), (784, 60), (1047, 110)],
            EventoSom.Colisao => [(220, 120), (110, 200)],
            EventoSom.EscudoQuebrado => [(880, 80), (330, 120)],
            EventoSom.Miragem => [(300, 70), (900, 70)],
            EventoSom.ChefeEntrou => [(140, 220), (90, 300)],
            EventoSom.ChefeDerrotado => [(392, 110), (659, 110), (1047, 220)],
            EventoSom.GameOver => [(440, 180), (330, 180), (220, 320)],
            EventoSom.NovoSetor => [(523, 90), (659, 90), (784, 140)],
            EventoSom.NovoCiclo => [(523, 100), (659, 100), (784, 100), (1047, 250)],
            EventoSom.Inicio => [(392, 80), (784, 120)],
            _ => []
        };

        if (notas.Length == 0) return;

        proximoSom = DateTime.Now.AddMilliseconds(notas.Sum(n => n.Item2) + 40);

        Task.Run(() =>
        {
            try
            {
                foreach (var (frequencia, duracao) in notas) Console.Beep(frequencia, duracao);
            }
            catch
            {
                // sem alto-falante disponivel
            }
        });
    }
}
