using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using JogoCorrida;

namespace JogoCorridaWinFormsApp
{
    /// <summary>
    /// Interface do jogo. A cena e desenhada primeiro em uma imagem na memoria e
    /// depois jogada na tela em fatias - e assim que sai o efeito de glitch.
    /// As regras ficam todas na classe <see cref="Jogo"/>; aqui so tem apresentacao.
    /// </summary>
    public partial class FormJogoCorrida : Form
    {
        private readonly Jogo jogo = new();
        private readonly Efeitos efeitos = new();
        private readonly Tutorial tutorial = new();
        private readonly Stopwatch relogio = Stopwatch.StartNew();
        private readonly HashSet<Keys> teclasPressionadas = [];
        private readonly Random rnd = new();

        private double ultimoQuadroMs;

        /// <summary>Telas que aparecem por cima do jogo, fora dos estados do motor.</summary>
        private enum TelaExtra { Nenhuma, DigitandoNome, Ranking }
        private TelaExtra telaExtra = TelaExtra.Nenhuma;
        private string nomeDigitado = "";
        private int posicaoConquistada;
        private double proximoGlitchMs;
        private double fimDoGlitchMs;

        private Bitmap? cena;            // onde o mundo e desenhado antes de ir para a tela
        private Bitmap? sobreposicao;    // scanlines + vinheta, prontas (nao mudam por quadro)

        private Font fonteTitulo = null!;
        private Font fonteGrande = null!;
        private Font fonteMedia = null!;
        private Font fonteFala = null!;
        private Font fonteMini = null!;
        private Font fonteHud = null!;

        private const float AlturaPainel = 92f;

        public FormJogoCorrida()
        {
            InitializeComponent();

            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            CriaFontes();
            Sons.Inicializar();

            jogo.Som += AoTocarSom;
            jogo.MelhorPontuacao = Ranking.Recorde;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            ConfiguraAreaDeJogo();
            jogo.IniciaJogo();
            tutorial.Reiniciar();
            Musica.Tocar(jogo.FaseAtual.Musica);

            ultimoQuadroMs = relogio.Elapsed.TotalMilliseconds;
            timerJogo.Enabled = true;
        }

        // =====================================================================
        // PREPARACAO
        // =====================================================================

        /// <summary>Ajusta as medidas do jogo ao tamanho real da janela.</summary>
        private void ConfiguraAreaDeJogo()
        {
            var cfg = jogo.Config;
            cfg.Largura = ClientSize.Width;
            cfg.Altura = ClientSize.Height;
            cfg.QtdFaixas = 3;
            cfg.MargemPista = (int)(cfg.Largura * 0.10);

            var faixa = cfg.LarguraFaixa;
            cfg.LarguraJogador = (int)(faixa * 0.56);
            // a altura acompanha a proporcao do desenho de Selene, para nao distorcer
            cfg.AlturaJogador = (int)(cfg.LarguraJogador * PixelArt.Proporcao("selene"));
            cfg.LarguraLagrima = cfg.AlturaLagrima = (int)(faixa * 0.34);
            cfg.LarguraPowerUp = cfg.AlturaPowerUp = (int)(faixa * 0.42);
            cfg.RecuoJogador = (int)(cfg.Altura * 0.13);
            cfg.VelocidadeLateral = faixa * 7.0;
            cfg.FolgaColisao = cfg.LarguraJogador * 0.18;

            PreparaImagens();
        }

        private void PreparaImagens()
        {
            cena?.Dispose();
            sobreposicao?.Dispose();

            cena = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
            sobreposicao = MontaSobreposicao(ClientSize.Width, ClientSize.Height);
        }

        /// <summary>Scanlines e vinheta sao sempre iguais, entao ficam prontas em uma imagem.</summary>
        private static Bitmap MontaSobreposicao(int largura, int altura)
        {
            var imagem = new Bitmap(Math.Max(1, largura), Math.Max(1, altura));
            using var g = Graphics.FromImage(imagem);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Tema.Vinheta(g, largura, altura, 165);
            Tema.Scanlines(g, largura, altura, 30);

            return imagem;
        }

        private void CriaFontes()
        {
            fonteTitulo = new Font("Impact", 38F, FontStyle.Regular);
            fonteGrande = new Font("Consolas", 21F, FontStyle.Bold);
            fonteMedia = new Font("Segoe UI", 12F, FontStyle.Bold);
            fonteFala = new Font("Consolas", 11.5F, FontStyle.Bold);
            fonteMini = new Font("Consolas", 8.5F, FontStyle.Bold);
            fonteHud = new Font("Consolas", 11F, FontStyle.Bold);
        }

        private void LiberarRecursos()
        {
            Musica.Parar();
            Sons.Parar();
            PixelArt.Liberar();

            cena?.Dispose();
            sobreposicao?.Dispose();

            fonteTitulo?.Dispose();
            fonteGrande?.Dispose();
            fonteMedia?.Dispose();
            fonteFala?.Dispose();
            fonteMini?.Dispose();
            fonteHud?.Dispose();
        }

        // =====================================================================
        // LACO PRINCIPAL
        // =====================================================================

        private void timerJogo_Tick(object? sender, EventArgs e)
        {
            var agora = relogio.Elapsed.TotalMilliseconds;
            var delta = agora - ultimoQuadroMs;
            ultimoQuadroMs = agora;

            // janela minimizada: nao da para desenhar nem remontar a area de jogo
            if (ClientSize.Width < 64 || ClientSize.Height < 64) return;

            if (jogo.Config.Largura != ClientSize.Width || jogo.Config.Altura != ClientSize.Height)
            {
                ConfiguraAreaDeJogo();
                jogo.IniciaJogo();
                tutorial.Reiniciar();
                efeitos.Limpar();
            }

            Demo.Jogar(jogo, delta);

            var estadoAnterior = jogo.Estado;
            var setorAnterior = jogo.NumeroSetor;
            var lagrimasAnteriores = jogo.Lagrimas;

            jogo.Atualizar(delta);
            efeitos.Atualizar(delta);
            tutorial.Observar(jogo);
            tutorial.Atualizar(delta, jogo.Estado == EstadoJogo.Jogando);
            Musica.Atualizar();

            if (jogo.Lagrimas > lagrimasAnteriores)
            {
                efeitos.Texto((float)jogo.Jogador.CentroX, (float)jogo.Jogador.PosicaoY - 12,
                              $"+{jogo.Config.PontosPorLagrima * jogo.Multiplicador}", Tema.Ciano);
            }

            if (jogo.NumeroSetor != setorAnterior) Musica.Tocar(jogo.FaseAtual.Musica);

            // efeito visual da batida
            if (estadoAnterior != EstadoJogo.Colidindo && jogo.Estado == EstadoJogo.Colidindo)
            {
                var atingido = jogo.ElementoAtingido;
                var x = atingido is null ? jogo.Jogador.CentroX : (atingido.CentroX + jogo.Jogador.CentroX) / 2;
                var y = atingido is null ? jogo.Jogador.CentroY : (atingido.CentroY + jogo.Jogador.PosicaoY) / 2;
                efeitos.Explosao((float)x, (float)y, jogo.Config.LarguraJogador / 46f);
            }

            if (estadoAnterior != jogo.Estado && jogo.VerificaFimJogo())
            {
                AoPerder();
            }
            if (estadoAnterior == EstadoJogo.GameOver && jogo.Estado != EstadoJogo.GameOver)
            {
                tutorial.Reiniciar();
            }

            AtualizaGlitch(agora);
            Invalidate();
        }

        /// <summary>No game over, pede o nome se a pontuacao entrou no ranking.</summary>
        private void AoPerder()
        {
            // o modo demonstracao nao entra no ranking
            if (!Demo.Ativo && Ranking.Qualifica(jogo.Pontuacao))
            {
                // comeca em branco: no campeonato cada jogador digita o seu
                nomeDigitado = "";
                telaExtra = TelaExtra.DigitandoNome;
            }
        }

        private void ConfirmaNome()
        {
            posicaoConquistada = Ranking.Registrar(nomeDigitado, jogo.Pontuacao, jogo.Ciclo, jogo.NumeroSetor);
            jogo.MelhorPontuacao = Ranking.Recorde;
            telaExtra = TelaExtra.Ranking;
        }

        private void AtualizaGlitch(double agora)
        {
            if (agora < proximoGlitchMs) return;
            fimDoGlitchMs = agora + rnd.Next(70, 220);
            proximoGlitchMs = agora + rnd.Next(1800, 5200);
        }

        /// <summary>Intensidade atual do glitch (0 a 1).</summary>
        private float ForcaDoGlitch()
        {
            var agora = relogio.Elapsed.TotalMilliseconds;
            var forca = 0f;

            if (agora < fimDoGlitchMs) forca = 0.45f;
            if (jogo.ChefeAtivo) forca = Math.Max(forca, 0.22f);
            if (jogo.ControlesInvertidos) forca = Math.Max(forca, 0.55f);
            if (jogo.Estado == EstadoJogo.Colidindo) forca = Math.Max(forca, (float)jogo.IntensidadeExplosao);
            if (jogo.Estado == EstadoJogo.TrocandoSetor) forca = Math.Max(forca, 0.35f);

            return forca;
        }

        private void AoTocarSom(EventoSom evento)
        {
            Sons.Tocar(evento);

            if (evento is EventoSom.ChefeEntrou or EventoSom.ChefeDerrotado or EventoSom.NovoCiclo)
            {
                fimDoGlitchMs = relogio.Elapsed.TotalMilliseconds + 500;
            }
        }

        // =====================================================================
        // TECLADO
        // =====================================================================

        private void FormJogoCorrida_KeyDown(object sender, KeyEventArgs e)
        {
            var repetida = !teclasPressionadas.Add(e.KeyCode);

            // enquanto digita o nome, o teclado e todo da caixa de texto
            if (telaExtra == TelaExtra.DigitandoNome)
            {
                if (!repetida) DigitaNome(e.KeyCode);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (telaExtra == TelaExtra.Ranking)
            {
                if (!repetida && e.KeyCode is Keys.Enter or Keys.Space or Keys.Escape or Keys.R)
                {
                    telaExtra = TelaExtra.Nenhuma;
                    posicaoConquistada = 0;
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (!repetida)
            {
                switch (e.KeyCode)
                {
                    case Keys.Left:
                    case Keys.A:
                        jogo.MoverEsquerda();
                        break;

                    case Keys.Right:
                    case Keys.D:
                        jogo.MoverDireita();
                        break;

                    case Keys.Enter:
                    case Keys.Space:
                        if (jogo.Estado == EstadoJogo.GameOver) { efeitos.Limpar(); tutorial.Reiniciar(); }
                        jogo.Confirmar();
                        if (jogo.Estado == EstadoJogo.Jogando) Musica.Retomar();
                        break;

                    case Keys.P:
                        jogo.AlternarPausa();
                        if (jogo.Estado == EstadoJogo.Pausado) Musica.Pausar(); else Musica.Retomar();
                        break;

                    case Keys.M:
                        Sons.Ligado = !Sons.Ligado;
                        Musica.Alternar(Sons.Ligado);
                        break;

                    case Keys.F2:
                        Demo.Alternar();
                        break;

                    case Keys.R:
                        // o ranking pode ser visto no menu e na tela de fim de jogo
                        if (jogo.Estado is EstadoJogo.Menu or EstadoJogo.GameOver)
                        {
                            telaExtra = TelaExtra.Ranking;
                        }
                        break;

                    case Keys.Escape:
                        if (jogo.Estado == EstadoJogo.Dialogo)
                        {
                            jogo.PularDialogo();
                        }
                        else if (jogo.Estado == EstadoJogo.Jogando)
                        {
                            jogo.AlternarPausa();
                            Musica.Pausar();
                        }
                        else Close();
                        break;
                }
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        /// <summary>Monta o nome do jogador letra por letra.</summary>
        private void DigitaNome(Keys tecla)
        {
            if (tecla == Keys.Enter)
            {
                ConfirmaNome();
                return;
            }

            if (tecla == Keys.Back)
            {
                if (nomeDigitado.Length > 0) nomeDigitado = nomeDigitado[..^1];
                return;
            }

            if (nomeDigitado.Length >= Ranking.TamanhoMaximoDoNome) return;

            char? letra = tecla switch
            {
                >= Keys.A and <= Keys.Z => (char)('A' + (tecla - Keys.A)),
                >= Keys.D0 and <= Keys.D9 => (char)('0' + (tecla - Keys.D0)),
                >= Keys.NumPad0 and <= Keys.NumPad9 => (char)('0' + (tecla - Keys.NumPad0)),
                Keys.Space => ' ',
                Keys.OemMinus or Keys.Subtract => '-',
                _ => null
            };

            if (letra.HasValue) nomeDigitado += letra.Value;
        }

        private void FormJogoCorrida_KeyUp(object sender, KeyEventArgs e)
            => teclasPressionadas.Remove(e.KeyCode);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timerJogo.Enabled = false;
            Musica.Parar();
            Sons.Parar();
            base.OnFormClosing(e);
        }

        // =====================================================================
        // DESENHO
        // =====================================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            if (cena is null || ClientSize.Width < 64 || ClientSize.Height < 64)
            {
                base.OnPaint(e);
                return;
            }

            using (var g = Graphics.FromImage(cena))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                var estado = g.Save();
                AplicaTremorDaBatida(g);

                DesenhaFundo(g);
                DesenhaPista(g);
                DesenhaElementos(g);
                DesenhaChefe(g);
                DesenhaJogador(g);
                tutorial.Desenhar(g, jogo, fonteMini);
                tutorial.DesenhaDicaInicial(g, jogo, fonteMini);
                efeitos.Desenhar(g, fonteMedia);
                DesenhaClaraoDaBatida(g);

                g.Restore(estado);
            }

            var tela = e.Graphics;
            tela.SmoothingMode = SmoothingMode.AntiAlias;
            tela.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            DesenhaCenaNaTela(tela);
            if (sobreposicao is not null) tela.DrawImage(sobreposicao, 0, 0);
            Tema.Ruido(tela, rnd, ClientSize.Width, ClientSize.Height, 60, 26);

            DesenhaPainel(tela);
            DesenhaAvisos(tela);
        }

        private void DesenhaCenaNaTela(Graphics tela)
        {
            if (cena is null) return;

            var forca = ForcaDoGlitch();
            if (forca <= 0.01f)
            {
                tela.DrawImage(cena, 0, 0);
                return;
            }

            var fatias = 16;
            var alturaFatia = (float)ClientSize.Height / fatias;

            for (int i = 0; i < fatias; i++)
            {
                var origem = new RectangleF(0, i * alturaFatia, ClientSize.Width, alturaFatia + 1);
                var desvio = rnd.NextDouble() < 0.45
                    ? (float)((rnd.NextDouble() * 2 - 1) * 22 * forca)
                    : 0f;

                var destino = new RectangleF(desvio, i * alturaFatia, ClientSize.Width, alturaFatia + 1);
                tela.DrawImage(cena, destino, origem, GraphicsUnit.Pixel);
            }

            using var rosa = new SolidBrush(Tema.ComAlfa(Tema.Rosa, (int)(40 * forca)));
            using var ciano = new SolidBrush(Tema.ComAlfa(Tema.Ciano, (int)(40 * forca)));
            tela.FillRectangle(rosa, -6 * forca, 0, ClientSize.Width, ClientSize.Height);
            tela.FillRectangle(ciano, 6 * forca, 0, ClientSize.Width, ClientSize.Height);
        }

        private void AplicaTremorDaBatida(Graphics g)
        {
            if (jogo.Estado != EstadoJogo.Colidindo) return;

            var forca = (float)(jogo.IntensidadeExplosao * jogo.Config.Largura * 0.03);
            g.TranslateTransform((float)(rnd.NextDouble() * 2 - 1) * forca,
                                 (float)(rnd.NextDouble() * 2 - 1) * forca);
        }

        // ---------------------------------------------------------------------
        // CENARIO
        // ---------------------------------------------------------------------

        private void DesenhaFundo(Graphics g)
        {
            var cfg = jogo.Config;
            var neon = Tema.De(jogo.FaseAtual.CorNeon);

            using (var fundo = new SolidBrush(Tema.De(jogo.FaseAtual.CorFundo)))
            {
                g.FillRectangle(fundo, -40, -40,
                                Math.Max(cfg.Largura, ClientSize.Width) + 80,
                                Math.Max(cfg.Altura, ClientSize.Height) + 80);
            }

            var periodo = cfg.Altura * 0.085f;
            var deslocamento = (float)(jogo.DistanciaPercorrida % periodo);

            using (var caneta = new Pen(Tema.ComAlfa(neon, 34), 1))
            {
                for (var y = -periodo + deslocamento; y < cfg.Altura + periodo; y += periodo)
                {
                    g.DrawLine(caneta, 0, y, cfg.MargemPista, y);
                    g.DrawLine(caneta, cfg.Largura - cfg.MargemPista, y, cfg.Largura, y);
                }
                for (float x = 0; x <= cfg.MargemPista; x += cfg.MargemPista / 3f)
                {
                    g.DrawLine(caneta, x, 0, x, cfg.Altura);
                    g.DrawLine(caneta, cfg.Largura - x, 0, cfg.Largura - x, cfg.Altura);
                }
            }

            Tema.Halo(g, neon, cfg.Largura / 2f, 0, cfg.Largura * 0.75f, 48);
        }

        private void DesenhaPista(Graphics g)
        {
            var cfg = jogo.Config;
            var neon = Tema.De(jogo.FaseAtual.CorNeon);

            using (var asfalto = new SolidBrush(Tema.De(jogo.FaseAtual.CorPista)))
            {
                g.FillRectangle(asfalto, cfg.MargemPista, -40, cfg.LarguraPista, cfg.Altura + 80);
            }

            Tema.LinhaNeon(g, neon, cfg.MargemPista, -40, cfg.MargemPista, cfg.Altura + 40, 2.5f);
            Tema.LinhaNeon(g, neon, cfg.MargemPista + cfg.LarguraPista, -40,
                           cfg.MargemPista + cfg.LarguraPista, cfg.Altura + 40, 2.5f);

            var comprimento = cfg.Altura * 0.05f;
            var vao = comprimento * 1.1f;
            var periodo = comprimento + vao;
            var deslocamento = (float)(jogo.DistanciaPercorrida % periodo);

            using (var pincel = new SolidBrush(Tema.ComAlfa(Tema.Branco, 150)))
            {
                for (int i = 1; i < cfg.QtdFaixas; i++)
                {
                    var x = cfg.MargemPista + cfg.LarguraFaixa * i;
                    for (var y = -periodo + deslocamento; y < cfg.Altura + periodo; y += periodo)
                    {
                        g.FillRectangle(pincel, x - 2, y, 4, comprimento);
                    }
                }
            }

            DesenhaMarcasDoAsfalto(g);
        }

        /// <summary>Setas pintadas no chao - nao afetam o jogo, so dao sensacao de velocidade.</summary>
        private void DesenhaMarcasDoAsfalto(Graphics g)
        {
            var cfg = jogo.Config;
            var neon = Tema.De(jogo.FaseAtual.CorNeon);
            var periodo = cfg.Altura * 0.42f;
            var deslocamento = (float)(jogo.DistanciaPercorrida % periodo);
            var indice = (int)(jogo.DistanciaPercorrida / periodo);

            using var caneta = new Pen(Tema.ComAlfa(neon, 38), 3f);
            var tamanho = cfg.LarguraFaixa * 0.22f;

            for (var y = -periodo + deslocamento; y < cfg.Altura + periodo; y += periodo)
            {
                var faixa = Math.Abs(indice + (int)(y / periodo)) % cfg.QtdFaixas;
                var centro = (float)cfg.CentroFaixa(faixa);

                for (int i = 0; i < 3; i++)
                {
                    var altura = y + i * tamanho * 0.9f;
                    g.DrawLine(caneta, centro - tamanho, altura + tamanho * 0.7f, centro, altura);
                    g.DrawLine(caneta, centro, altura, centro + tamanho, altura + tamanho * 0.7f);
                }
            }
        }

        // ---------------------------------------------------------------------
        // ELEMENTOS
        // ---------------------------------------------------------------------

        private void DesenhaElementos(Graphics g)
        {
            foreach (var el in jogo.Elementos)
            {
                var r = new RectangleF((float)el.PosicaoX, (float)el.PosicaoY, el.Largura, el.Altura);
                if (r.Bottom < -40 || r.Top > jogo.Config.Altura + 40) continue;

                switch (el.Tipo)
                {
                    case TipoElemento.Lagrima:
                        Marcador(g, r, Tema.Ciano, 0.55f);
                        var pulso = 1 + (float)Math.Sin(el.Animacao / 220.0) * 0.08f;
                        PixelArt.Desenhar(g, "lagrima", Escalar(r, pulso));
                        break;

                    case TipoElemento.PowerUp:
                        Marcador(g, r, Tema.Dourado, 0.75f);
                        PixelArt.Desenhar(g, SpriteDoPoder(el.PowerUp),
                                          Escalar(r, 1 + (float)Math.Sin(el.Animacao / 260.0) * 0.07f));
                        break;

                    case TipoElemento.Obstaculo:
                        DesenhaObstaculo(g, el, r);
                        break;
                }
            }
        }

        private void DesenhaObstaculo(Graphics g, Elemento el, RectangleF r)
        {
            // a miragem nao machuca: marcador ciano em vez de vermelho
            var perigoso = el.Obstaculo != TipoObstaculo.Miragem;
            Marcador(g, r, perigoso ? Tema.Rosa : Tema.Ciano, perigoso ? 0.6f : 0.4f);

            var sprite = el.Obstaculo switch
            {
                TipoObstaculo.Vulto => jogo.FaseAtual.SpriteVulto,
                TipoObstaculo.Executor => "pesado",
                TipoObstaculo.Drone => "drone",
                TipoObstaculo.Barreira => "barreira",
                _ => "miragem"
            };

            var caixa = r;
            if (el.Obstaculo == TipoObstaculo.Drone)
            {
                var flutuar = (float)Math.Sin(el.Animacao / 120.0) * r.Height * 0.08f;
                caixa = new RectangleF(r.X, r.Y + flutuar, r.Width, r.Height);
            }

            PixelArt.Desenhar(g, sprite, caixa);
        }

        /// <summary>
        /// Mancha de luz no chao embaixo do elemento. E o aviso mais rapido de todos:
        /// rosa = perigo, ciano = pode pegar, dourado = poder.
        /// </summary>
        private static void Marcador(Graphics g, RectangleF r, Color cor, float forca)
        {
            var largura = r.Width * 1.15f;
            var altura = r.Height * 0.3f;
            var caixa = new RectangleF(r.X + (r.Width - largura) / 2, r.Bottom - altura * 0.75f, largura, altura);

            using (var sombra = new SolidBrush(Tema.ComAlfa(cor, (int)(70 * forca))))
            {
                g.FillEllipse(sombra, caixa);
            }
            using var borda = new Pen(Tema.ComAlfa(cor, (int)(150 * forca)), 2);
            g.DrawEllipse(borda, caixa);
        }

        private static RectangleF Escalar(RectangleF r, float fator)
            => new(r.X - r.Width * (fator - 1) / 2, r.Y - r.Height * (fator - 1) / 2,
                   r.Width * fator, r.Height * fator);

        private static string SpriteDoPoder(TipoPowerUp poder) => poder switch
        {
            TipoPowerUp.Empatia => "empatia",
            TipoPowerUp.Proposito => "proposito",
            TipoPowerUp.Conexao => "conexao",
            _ => "visao"
        };

        // ---------------------------------------------------------------------
        // SELENE E CHEFE
        // ---------------------------------------------------------------------

        private void DesenhaJogador(Graphics g)
        {
            var cfg = jogo.Config;
            var jogador = jogo.Jogador;

            if (jogo.Invencivel && jogo.Estado == EstadoJogo.Jogando
                && (int)(jogo.TempoInvencivelMs / 110) % 2 == 0)
            {
                return;
            }

            var r = new RectangleF((float)jogador.PosicaoX, (float)jogador.PosicaoY, jogador.Largura, jogador.Altura);
            var alvo = jogo.PosicionaObjeto(jogo.FaixaAlvo);
            var inclinacao = (float)Math.Clamp((alvo - jogador.PosicaoX) / cfg.LarguraFaixa * 16, -12, 12);

            // rastro de luz atras
            using (var rastro = new LinearGradientBrush(
                       new RectangleF(r.X, r.Bottom - 4, Math.Max(1, r.Width), Math.Max(1, cfg.Altura - r.Bottom + 8)),
                       Tema.ComAlfa(Tema.Ciano, 110), Tema.ComAlfa(Tema.Ciano, 0),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(rastro, r.X + r.Width * 0.25f, r.Bottom - 4,
                                r.Width * 0.5f, cfg.Altura - r.Bottom + 8);
            }

            if (jogo.TurboAtivo) DesenhaLinhasDeVelocidade(g, r);

            Marcador(g, r, Tema.Ciano, 1f);
            PixelArt.Desenhar(g, "selene", r, inclinacao);

            if (jogo.TemEscudo) DesenhaEscudo(g, r);
            if (jogo.ImaAtivo) DesenhaCampoDoIma(g, r);
        }

        private void DesenhaLinhasDeVelocidade(Graphics g, RectangleF r)
        {
            using var caneta = new Pen(Tema.ComAlfa(Tema.Dourado, 130), 2);
            for (int i = 0; i < 6; i++)
            {
                var x = r.X - r.Width * 0.4f + (float)rnd.NextDouble() * r.Width * 1.8f;
                var y = r.Bottom + (float)rnd.NextDouble() * 60;
                g.DrawLine(caneta, x, y, x, y + 18);
            }
        }

        private void DesenhaEscudo(Graphics g, RectangleF r)
        {
            var caixa = RectangleF.Inflate(r, r.Width * 0.3f, r.Height * 0.2f);
            var pulso = (int)(90 + Math.Sin(relogio.Elapsed.TotalMilliseconds / 160.0) * 50);

            using var caneta = new Pen(Tema.ComAlfa(Tema.Dourado, pulso), 3);
            g.DrawEllipse(caneta, caixa);
            using var preenchimento = new SolidBrush(Tema.ComAlfa(Tema.Dourado, 26));
            g.FillEllipse(preenchimento, caixa);
        }

        private void DesenhaCampoDoIma(Graphics g, RectangleF r)
        {
            var raio = (float)(jogo.Config.Altura * 0.45);
            var centroX = r.X + r.Width / 2;
            var centroY = r.Y + r.Height / 2;
            var pulso = (float)(Math.Sin(relogio.Elapsed.TotalMilliseconds / 260.0) * 0.06 + 1);

            using var caneta = new Pen(Tema.ComAlfa(Tema.Dourado, 45), 2);
            g.DrawEllipse(caneta, centroX - raio * pulso, centroY - raio * pulso,
                          raio * 2 * pulso, raio * 2 * pulso);
        }

        private void DesenhaChefe(Graphics g)
        {
            var chefe = jogo.Chefe;
            if (chefe is null) return;

            var r = new RectangleF((float)chefe.PosicaoX, (float)chefe.PosicaoY, chefe.Largura, chefe.Altura);
            var pulso = (float)(Math.Sin(chefe.Animacao / 320.0) * 0.04 + 1);

            Tema.Halo(g, Tema.Rosa, r.X + r.Width / 2, r.Y + r.Height / 2, r.Width * 0.9f, 110);
            PixelArt.Desenhar(g, jogo.FaseAtual.SpriteChefe, Escalar(r, pulso));
        }

        private void DesenhaClaraoDaBatida(Graphics g)
        {
            var intensidade = jogo.IntensidadeExplosao;
            if (intensidade <= 0) return;

            var alfa = (int)(210 * Math.Pow(intensidade, 3));
            if (alfa <= 0) return;

            using var clarao = new SolidBrush(Color.FromArgb(alfa, 255, 110, 170));
            g.FillRectangle(clarao, -40, -40, jogo.Config.Largura + 80, jogo.Config.Altura + 80);
        }

        // =====================================================================
        // PAINEL (HUD)
        // =====================================================================

        private void DesenhaPainel(Graphics g)
        {
            var cfg = jogo.Config;
            var largura = cfg.Largura;
            var meio = largura / 2f;
            var fase = jogo.FaseAtual;
            var neon = Tema.De(fase.CorNeon);

            using (var fundo = new SolidBrush(Color.FromArgb(238, 5, 5, 10)))
            {
                g.FillRectangle(fundo, 0, 0, largura, AlturaPainel);
            }
            Tema.LinhaNeon(g, neon, 0, AlturaPainel, largura, AlturaPainel, 1.5f, 0.8f);

            Tema.Texto(g, "[ PONTOS ]", fonteMini, Tema.ComAlfa(Tema.Cinza, 220), 14, 5, StringAlignment.Near);
            Tema.Texto(g, "RECORDE", fonteMini, Tema.ComAlfa(Tema.Cinza, 220), meio, 5, StringAlignment.Center);

            Tema.TextoGlitch(g, jogo.Pontuacao.ToString("N0"), fonteGrande, Tema.Branco,
                             12, 17, StringAlignment.Near, 1.5f);
            Tema.Texto(g, jogo.MelhorPontuacao.ToString("N0"), fonteHud, Tema.ComAlfa(Tema.Branco, 220),
                       meio, 19, StringAlignment.Center);

            // vidas: uma Selene para cada tentativa que ainda resta
            var x = largura - 16f;
            for (int i = 0; i < jogo.Vidas; i++)
            {
                PixelArt.Desenhar(g, "selene", new RectangleF(x - 15, 4, 14, 16));
                x -= 18;
            }

            // lagrimas coletadas (icone diferente do das vidas, para nao confundir)
            PixelArt.Desenhar(g, "lagrima", new RectangleF(largura - 58, 24, 13, 14));
            Tema.Texto(g, $"x{jogo.Lagrimas}", fonteMini, Tema.Ciano, largura - 14, 26, StringAlignment.Far);

            // setor e ciclo
            Tema.Texto(g, $"SETOR {jogo.NumeroSetor}/3  {fase.Nome}", fonteMini,
                       Tema.ComAlfa(Tema.Branco, 235), 14, 52, StringAlignment.Near);
            Tema.Texto(g, $"CICLO {jogo.Ciclo}", fonteMini,
                       jogo.Ciclo > 1 ? Tema.Rosa : Tema.ComAlfa(Tema.Cinza, 235),
                       largura - 14, 52, StringAlignment.Far);

            DesenhaPoderesAtivos(g, meio, 46);

            if (jogo.Multiplicador > 1)
            {
                Tema.Texto(g, "PONTOS x2", fonteMini, Tema.Dourado, 14, 68, StringAlignment.Near);
            }
            else if (Demo.Ativo)
            {
                Tema.Texto(g, "DEMO (F2)", fonteMini, Tema.ComAlfa(Tema.Verde, 220), 14, 68, StringAlignment.Near);
            }

            if (jogo.ControlesInvertidos)
            {
                Tema.Texto(g, "CONTROLES INVERTIDOS", fonteMini, Tema.Ciano, largura - 14, 68, StringAlignment.Far);
            }

            var y = AlturaPainel - 6;
            if (jogo.ChefeAtivo)
            {
                Tema.Texto(g, $"!! {fase.NomeChefe} !!", fonteMini, Tema.Rosa, meio, 68, StringAlignment.Center);

                using (var trilho = new SolidBrush(Color.FromArgb(90, 60, 10, 25)))
                {
                    g.FillRectangle(trilho, 0, y, largura, 5);
                }
                Tema.BarraNeon(g, Tema.Rosa,
                               new RectangleF(0, y, (float)(largura * jogo.EnergiaChefe / 100.0), 5), 1.2f);
            }
            else
            {
                using (var trilho = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                {
                    g.FillRectangle(trilho, 0, y, largura, 5);
                }
                Tema.BarraNeon(g, neon, new RectangleF(0, y, (float)(largura * jogo.ProgressoSetor), 5), 0.9f);
            }
        }

        private void DesenhaPoderesAtivos(Graphics g, float centroX, float y)
        {
            var ativos = new List<TipoPowerUp>();
            if (jogo.TemEscudo) ativos.Add(TipoPowerUp.Empatia);
            foreach (var poder in Enum.GetValues<TipoPowerUp>())
            {
                if (poder != TipoPowerUp.Empatia && jogo.TempoDoPoder(poder) > 0) ativos.Add(poder);
            }
            if (ativos.Count == 0) return;

            var x = centroX - ativos.Count * 13;
            foreach (var poder in ativos)
            {
                PixelArt.Desenhar(g, SpriteDoPoder(poder), new RectangleF(x, y, 18, 18));

                var tempo = jogo.TempoDoPoder(poder);
                if (tempo > 0)
                {
                    Tema.Texto(g, $"{tempo / 1000:F0}", fonteMini, Tema.ComAlfa(Tema.Dourado, 230),
                               x + 9, y + 17, StringAlignment.Center);
                }
                x += 26;
            }
        }

        // =====================================================================
        // TELAS
        // =====================================================================

        private void DesenhaAvisos(Graphics g)
        {
            // as telas de ranking ficam por cima de tudo
            if (telaExtra == TelaExtra.DigitandoNome) { DesenhaDigitacaoDoNome(g); return; }
            if (telaExtra == TelaExtra.Ranking) { DesenhaRanking(g); return; }

            switch (jogo.Estado)
            {
                case EstadoJogo.Menu: DesenhaMenu(g); break;
                case EstadoJogo.Dialogo: DesenhaDialogo(g); break;
                case EstadoJogo.TrocandoSetor: DesenhaTrocaDeSetor(g); break;
                case EstadoJogo.GameOver: DesenhaFimDeJogo(g); break;

                case EstadoJogo.Pausado:
                    DesenhaCaixa(g, "PAUSA", Tema.Branco,
                    [
                        "ENTER  continuar",
                        "ESC  sair do jogo",
                        Sons.Ligado ? "M  desligar o som" : "M  ligar o som"
                    ]);
                    break;
            }
        }

        private void DesenhaMenu(Graphics g)
        {
            var cfg = jogo.Config;
            EscureceTela(g, 190);

            var y = AlturaPainel + 16;

            Tema.TextoGlitch(g, "POST HUMAN", fonteTitulo, Tema.Branco, cfg.Largura / 2f, y, StringAlignment.Center, 3f);
            y += cfg.Altura * 0.072f;
            Tema.TextoGlitch(g, "RUN", fonteTitulo, Tema.Rosa, cfg.Largura / 2f, y, StringAlignment.Center, 3f);
            y += cfg.Altura * 0.08f;

            Tema.Texto(g, "voce e SELENE (NYX-01).", fonteMini, Tema.ComAlfa(Tema.Branco, 235),
                       cfg.Largura / 2f, y, StringAlignment.Center);
            y += 18;
            Tema.Texto(g, "corra da Nex Gen e encontre Sof.", fonteMini, Tema.ComAlfa(Tema.Branco, 235),
                       cfg.Largura / 2f, y, StringAlignment.Center);

            y += 30;
            DesenhaLegenda(g, y);

            var piscando = (int)(relogio.Elapsed.TotalMilliseconds / 450) % 2 == 0;
            if (piscando)
            {
                Tema.TextoGlitch(g, "> ENTER PARA CORRER <", fonteMedia, Tema.Ciano,
                                 cfg.Largura / 2f, cfg.Altura * 0.80f, StringAlignment.Center);
            }

            Tema.Texto(g, "SETAS mudar de faixa   P pausa   M som   R ranking   F2 demo", fonteMini,
                       Tema.ComAlfa(Tema.Cinza, 235), cfg.Largura / 2f, cfg.Altura * 0.86f, StringAlignment.Center);

            // os tres primeiros do ranking, direto no menu
            var melhores = Ranking.Colocacoes.Take(3).ToList();
            var linha = cfg.Altura * 0.905f;
            for (int i = 0; i < melhores.Count; i++)
            {
                Tema.Texto(g, $"{i + 1}. {melhores[i].Nome}  {melhores[i].Pontos:N0}", fonteMini,
                           i == 0 ? Tema.Dourado : Tema.ComAlfa(Tema.Branco, 215),
                           cfg.Largura / 2f, linha, StringAlignment.Center);
                linha += 17;
            }
        }

        /// <summary>
        /// A legenda do menu: mostra o desenho de cada coisa com o rotulo do que fazer.
        /// Fundo vermelho = fuja; fundo ciano = pegue.
        /// </summary>
        private void DesenhaLegenda(Graphics g, float y)
        {
            var cfg = jogo.Config;
            var tamanho = cfg.Largura * 0.085f;

            void Grupo(string titulo, Color cor, string[] sprites, string rotulo, float topo)
            {
                var caixa = new RectangleF(cfg.Largura * 0.08f, topo,
                                           cfg.Largura * 0.84f, tamanho * 1.9f);

                using (var fundo = new SolidBrush(Tema.ComAlfa(cor, 26)))
                using (var caminho = Tema.Arredondado(caixa, 4))
                {
                    g.FillPath(fundo, caminho);
                }
                using (var borda = new Pen(Tema.ComAlfa(cor, 150), 1.5f))
                using (var caminho = Tema.Arredondado(caixa, 4))
                {
                    g.DrawPath(borda, caminho);
                }

                Tema.Texto(g, titulo, fonteMini, cor, caixa.X + 10, caixa.Y + 6, StringAlignment.Near);

                var x = caixa.X + 12;
                foreach (var sprite in sprites)
                {
                    var altura = tamanho * PixelArt.Proporcao(sprite);
                    PixelArt.Desenhar(g, sprite, new RectangleF(x, caixa.Bottom - altura - 8, tamanho, altura));
                    x += tamanho + 8;
                }

                Tema.Texto(g, rotulo, fonteMini, Tema.ComAlfa(Tema.Branco, 230),
                           caixa.Right - 10, caixa.Bottom - 22, StringAlignment.Far);
            }

            Grupo("VOCE", Tema.Ciano, ["selene"], "Selene (NYX-01)", y);
            Grupo("FUJA", Tema.Rosa, ["agente", "neosage", "angeldust", "pesado", "barreira"],
                  "encostou, perdeu vida", y + tamanho * 2.1f);
            Grupo("PEGUE", Tema.Dourado, ["lagrima", "empatia", "proposito", "conexao", "visao"],
                  "lagrimas e Insights", y + tamanho * 4.2f);
        }

        /// <summary>Caixa de conversa com o retrato do personagem.</summary>
        private void DesenhaDialogo(Graphics g)
        {
            var cfg = jogo.Config;
            var fala = jogo.FalaAtual;
            if (fala is null) return;

            EscureceTela(g, 210);

            var cor = Tema.De(fala.Cor);
            var alturaCaixa = cfg.Altura * 0.30f;
            var caixa = new RectangleF(cfg.Largura * 0.06f, cfg.Altura * 0.46f,
                                       cfg.Largura * 0.88f, alturaCaixa);

            using (var fundo = new SolidBrush(Color.FromArgb(242, 8, 7, 14)))
            using (var caminho = Tema.Arredondado(caixa, 4))
            {
                g.FillPath(fundo, caminho);
            }
            Tema.LinhaNeon(g, cor, caixa.X, caixa.Y, caixa.Right, caixa.Y, 2f);
            Tema.LinhaNeon(g, cor, caixa.X, caixa.Bottom, caixa.Right, caixa.Bottom, 2f);

            // retrato
            var ladoRetrato = alturaCaixa * 0.46f;
            var retrato = new RectangleF(caixa.X + 14, caixa.Y + 16, ladoRetrato, ladoRetrato);
            Tema.Halo(g, cor, retrato.X + retrato.Width / 2, retrato.Y + retrato.Height / 2,
                      retrato.Width * 0.75f, 45);
            PixelArt.Desenhar(g, fala.Retrato, retrato);

            // nome
            var textoX = retrato.Right + 16;
            Tema.Texto(g, fala.Personagem, fonteMedia, cor, textoX, caixa.Y + 12, StringAlignment.Near);

            // texto aparecendo letra por letra
            var visiveis = (int)Math.Clamp(jogo.TempoFalaMs / 16.0, 0, fala.Texto.Length);
            var texto = fala.Texto[..visiveis];
            DesenhaTextoQuebrado(g, texto, fonteFala, Tema.Branco,
                                 textoX, caixa.Y + 40, caixa.Right - textoX - 14);

            // indicador de avancar
            if ((int)(relogio.Elapsed.TotalMilliseconds / 400) % 2 == 0)
            {
                Tema.Texto(g, "ENTER >", fonteMini, Tema.ComAlfa(Tema.Branco, 200),
                           caixa.Right - 14, caixa.Bottom - 24, StringAlignment.Far);
            }

            if (jogo.IndiceFala == 0)
            {
                Tema.Texto(g, "ESC pula", fonteMini, Tema.ComAlfa(Tema.Cinza, 190),
                           (caixa.X + caixa.Right) / 2, caixa.Bottom - 24, StringAlignment.Center);
            }

            // quantas falas faltam
            Tema.Texto(g, $"{jogo.IndiceFala + 1}/{jogo.DialogoAtual.Count}", fonteMini,
                       Tema.ComAlfa(Tema.Cinza, 200), caixa.X + 14, caixa.Bottom - 24, StringAlignment.Near);
        }

        /// <summary>Escreve o texto quebrando as linhas na largura disponivel.</summary>
        private static void DesenhaTextoQuebrado(Graphics g, string texto, Font fonte, Color cor,
                                                 float x, float y, float largura)
        {
            var palavras = texto.Split(' ');
            var linha = "";
            var alturaLinha = fonte.Height + 2;

            foreach (var palavra in palavras)
            {
                var teste = linha.Length == 0 ? palavra : linha + " " + palavra;
                if (g.MeasureString(teste, fonte).Width > largura && linha.Length > 0)
                {
                    Tema.Texto(g, linha, fonte, cor, x, y, StringAlignment.Near);
                    y += alturaLinha;
                    linha = palavra;
                }
                else
                {
                    linha = teste;
                }
            }
            if (linha.Length > 0) Tema.Texto(g, linha, fonte, cor, x, y, StringAlignment.Near);
        }

        private void DesenhaTrocaDeSetor(Graphics g)
        {
            var cfg = jogo.Config;
            var fase = jogo.FaseAtual;

            var restante = jogo.TempoEfeitoMs;
            var total = cfg.DuracaoTrocaSetorMs;
            var visivel = Math.Clamp(Math.Min((total - restante) / 250.0, restante / 400.0), 0, 1);

            EscureceTela(g, (int)(175 * visivel));

            var cor = Tema.ComAlfa(Tema.Branco, (int)(255 * visivel));
            var destaque = Tema.ComAlfa(Tema.Ciano, (int)(255 * visivel));

            Tema.TextoGlitch(g, $"CICLO {jogo.Ciclo}", fonteTitulo, destaque,
                             cfg.Largura / 2f, cfg.Altura * 0.34f, StringAlignment.Center, 3f);
            Tema.Texto(g, fase.Nome, fonteMedia, cor, cfg.Largura / 2f, cfg.Altura * 0.47f, StringAlignment.Center);
            Tema.Texto(g, fase.Descricao, fonteMini, cor, cfg.Largura / 2f, cfg.Altura * 0.53f, StringAlignment.Center);

            if (jogo.AvisoDeCiclo != "")
            {
                Tema.Texto(g, $"M8: {jogo.AvisoDeCiclo}", fonteMini,
                           Tema.ComAlfa(Tema.Ciano, (int)(255 * visivel)),
                           cfg.Largura / 2f, cfg.Altura * 0.60f, StringAlignment.Center);
            }
        }

        private void DesenhaFimDeJogo(Graphics g)
        {
            var novoRecorde = jogo.Pontuacao >= jogo.MelhorPontuacao && jogo.Pontuacao > 0;

            string[] linhas =
            [
                $"PONTUACAO  {jogo.Pontuacao:N0}",
                "",
                $"chegou ate   ciclo {jogo.Ciclo}, setor {jogo.NumeroSetor}",
                $"tempo        {(int)jogo.PontosTempo:N0}",
                $"obstaculos   {jogo.ObstaculosSuperados}  ({(int)jogo.PontosObstaculos:N0})",
                $"lagrimas     {jogo.Lagrimas}  ({(int)jogo.PontosLagrimas:N0})",
                $"Insights     {jogo.PowerUpsPegos}",
                "",
                novoRecorde ? "*** NOVO RECORDE ***" : $"recorde {jogo.MelhorPontuacao:N0}",
                "",
                "ENTER  correr de novo    R  ranking    ESC  sair"
            ];

            DesenhaCaixa(g, "GAME OVER", Tema.Rosa, linhas);
        }

        private void DesenhaCaixa(Graphics g, string titulo, Color corTitulo, string[] linhas)
        {
            var cfg = jogo.Config;
            EscureceTela(g, 195);

            var alturaCaixa = 118 + linhas.Length * 22;
            var caixa = new RectangleF(cfg.Largura * 0.06f,
                                       (cfg.Altura - alturaCaixa) / 2f,
                                       cfg.Largura * 0.88f,
                                       alturaCaixa);

            using (var fundo = new SolidBrush(Color.FromArgb(235, 8, 7, 14)))
            using (var caminho = Tema.Arredondado(caixa, 4))
            {
                g.FillPath(fundo, caminho);
            }
            Tema.LinhaNeon(g, corTitulo, caixa.X, caixa.Y, caixa.Right, caixa.Y, 2f);
            Tema.LinhaNeon(g, corTitulo, caixa.X, caixa.Bottom, caixa.Right, caixa.Bottom, 2f);

            Tema.TextoGlitch(g, titulo, fonteTitulo, corTitulo, cfg.Largura / 2f, caixa.Y + 20,
                             StringAlignment.Center, 3f);

            var y = caixa.Y + 100;
            foreach (var linha in linhas)
            {
                var destaque = linha.StartsWith("PONTUACAO") || linha.StartsWith("***");
                Tema.Texto(g, linha, destaque ? fonteMedia : fonteMini,
                           destaque ? Tema.Ciano : Tema.ComAlfa(Tema.Branco, 230),
                           cfg.Largura / 2f, y, StringAlignment.Center);
                y += 22;
            }
        }

        /// <summary>Caixa para o jogador digitar o nome depois de entrar no ranking.</summary>
        private void DesenhaDigitacaoDoNome(Graphics g)
        {
            var cfg = jogo.Config;
            EscureceTela(g, 205);

            var caixa = new RectangleF(cfg.Largura * 0.08f, cfg.Altura * 0.30f,
                                       cfg.Largura * 0.84f, cfg.Altura * 0.34f);

            using (var fundo = new SolidBrush(Color.FromArgb(240, 8, 7, 14)))
            using (var caminho = Tema.Arredondado(caixa, 4))
            {
                g.FillPath(fundo, caminho);
            }
            Tema.LinhaNeon(g, Tema.Dourado, caixa.X, caixa.Y, caixa.Right, caixa.Y, 2f);
            Tema.LinhaNeon(g, Tema.Dourado, caixa.X, caixa.Bottom, caixa.Right, caixa.Bottom, 2f);

            var meio = cfg.Largura / 2f;
            Tema.TextoGlitch(g, "ENTROU NO RANKING!", fonteMedia, Tema.Dourado, meio, caixa.Y + 20,
                             StringAlignment.Center, 2f);
            Tema.Texto(g, $"{jogo.Pontuacao:N0} pontos  -  ciclo {jogo.Ciclo}", fonteMini,
                       Tema.ComAlfa(Tema.Branco, 235), meio, caixa.Y + 52, StringAlignment.Center);
            Tema.Texto(g, "digite seu nome:", fonteMini, Tema.ComAlfa(Tema.Cinza, 235),
                       meio, caixa.Y + 78, StringAlignment.Center);

            // campo de texto
            var campo = new RectangleF(caixa.X + 30, caixa.Y + 104, caixa.Width - 60, 40);
            using (var fundoCampo = new SolidBrush(Color.FromArgb(200, 18, 16, 26)))
            {
                g.FillRectangle(fundoCampo, campo);
            }
            using (var borda = new Pen(Tema.ComAlfa(Tema.Ciano, 190), 2))
            {
                g.DrawRectangle(borda, campo.X, campo.Y, campo.Width, campo.Height);
            }

            var cursor = (int)(relogio.Elapsed.TotalMilliseconds / 450) % 2 == 0 ? "_" : " ";
            Tema.Texto(g, nomeDigitado + cursor, fonteMedia, Tema.Branco,
                       campo.X + campo.Width / 2, campo.Y + 8, StringAlignment.Center);

            Tema.Texto(g, "ENTER confirma   BACKSPACE apaga", fonteMini,
                       Tema.ComAlfa(Tema.Cinza, 235), meio, caixa.Bottom - 30, StringAlignment.Center);
        }

        /// <summary>Lista dos dez melhores, lida do arquivo ranking.csv.</summary>
        private void DesenhaRanking(Graphics g)
        {
            var cfg = jogo.Config;
            EscureceTela(g, 210);

            var meio = cfg.Largura / 2f;
            var colocacoes = Ranking.Colocacoes;

            Tema.TextoGlitch(g, "RANKING", fonteTitulo, Tema.Dourado, meio, AlturaPainel + 18,
                             StringAlignment.Center, 3f);

            var y = AlturaPainel + 86;
            var alturaLinha = Math.Min(30f, (cfg.Altura * 0.56f) / Math.Max(1, colocacoes.Count));

            if (colocacoes.Count == 0)
            {
                Tema.Texto(g, "ninguem correu ainda.", fonteMini, Tema.ComAlfa(Tema.Branco, 220),
                           meio, y, StringAlignment.Center);
            }

            for (int i = 0; i < colocacoes.Count; i++)
            {
                var c = colocacoes[i];
                var novo = posicaoConquistada == i + 1;
                var cor = novo ? Tema.Dourado
                        : i == 0 ? Tema.Ciano
                        : Tema.ComAlfa(Tema.Branco, 225);

                if (novo)
                {
                    using var destaque = new SolidBrush(Tema.ComAlfa(Tema.Dourado, 30));
                    g.FillRectangle(destaque, cfg.Largura * 0.06f, y - 3, cfg.Largura * 0.88f, alturaLinha);
                }

                Tema.Texto(g, $"{i + 1,2}", fonteHud, cor, cfg.Largura * 0.10f, y, StringAlignment.Near);
                Tema.Texto(g, c.Nome, fonteHud, cor, cfg.Largura * 0.20f, y, StringAlignment.Near);
                Tema.Texto(g, c.Pontos.ToString("N0"), fonteHud, cor, cfg.Largura * 0.76f, y, StringAlignment.Far);
                Tema.Texto(g, $"c{c.Ciclo}", fonteMini, Tema.ComAlfa(Tema.Cinza, 220),
                           cfg.Largura * 0.90f, y + 3, StringAlignment.Far);

                y += alturaLinha;
            }

            Tema.Texto(g, "ENTER para voltar", fonteMini, Tema.Ciano, meio, cfg.Altura * 0.90f,
                       StringAlignment.Center);
        }

        private void EscureceTela(Graphics g, int alfa)
        {
            using var escuro = new SolidBrush(Color.FromArgb(Math.Clamp(alfa, 0, 255), 4, 3, 8));
            g.FillRectangle(escuro, 0, 0, jogo.Config.Largura, jogo.Config.Altura);
        }

    }
}
