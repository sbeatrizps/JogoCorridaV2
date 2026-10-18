namespace JogoCorrida
{
    /// <summary>
    /// Motor do jogo: regras, movimento, pontuacao, power-ups, setores e chefes.
    /// Avisa a interface sobre os eventos de som, mas nao desenha nada.
    ///
    /// O jogo NAO TEM FIM: os tres setores se repetem em ciclos, cada volta mais
    /// rapida, ate o jogador perder as vidas - como no joguinho do dinossauro.
    /// </summary>
    public class Jogo
    {
        private readonly Random rnd = new();

        private int indiceFase;
        private double pontosDaFase;
        private double tempoEfeitoMs;
        private double tempoInvencivelMs;
        private double tempoMiragemMs;

        // ----- controle das ondas de obstaculos -----
        private double distanciaProximaOnda;
        private List<int> faixasLivresAnteriores = [];
        private Elemento? referenciaOndaAnterior;
        private int ancoraAnterior;
        private int repeticoesCorredor;

        // ----- dialogos -----
        private readonly List<Fala> dialogo = [];
        private readonly HashSet<string> dialogosVistos = [];
        private int indiceFala;
        private double tempoFalaMs;

        // ----- CONFIGURACAO -----
        public Configuracao Config { get; set; } = new();
        public List<Fase> Fases { get; set; } = Fase.FasesPadrao();

        // ----- ELEMENTOS -----
        public Elemento Jogador { get; private set; } = new();
        public List<Elemento> Elementos { get; } = [];
        public Elemento? Chefe { get; private set; }

        public IEnumerable<Elemento> Obstaculos => Elementos.Where(e => e.Tipo == TipoElemento.Obstaculo);

        // ----- ESTADO -----
        public EstadoJogo Estado { get; private set; } = EstadoJogo.Menu;
        public int Vidas { get; private set; }
        public double VelocidadeEstrada { get; private set; }
        public double DistanciaPercorrida { get; private set; }
        public double TempoJogoMs { get; private set; }
        public int FaixaAlvo { get; private set; }

        /// <summary>Quantas voltas completas pelos tres setores ja aconteceram (comeca em 1).</summary>
        public int Ciclo { get; private set; } = 1;

        // ----- PONTUACAO -----
        public double PontosTempo { get; private set; }
        public double PontosObstaculos { get; private set; }
        public double PontosLagrimas { get; private set; }
        public int Pontuacao => (int)(PontosTempo + PontosObstaculos + PontosLagrimas);
        public int Lagrimas { get; private set; }
        public int ObstaculosSuperados { get; private set; }
        public int PowerUpsPegos { get; private set; }
        public int MelhorPontuacao { get; set; }

        // ----- PODERES (os quatro Insights) -----
        private readonly Dictionary<TipoPowerUp, double> poderes = [];
        public bool TemEscudo { get; private set; }
        public bool TurboAtivo => TempoDoPoder(TipoPowerUp.Proposito) > 0;
        public bool ImaAtivo => TempoDoPoder(TipoPowerUp.Conexao) > 0;
        public bool LentidaoAtiva => TempoDoPoder(TipoPowerUp.Visao) > 0;
        public double TempoDoPoder(TipoPowerUp poder) => poderes.TryGetValue(poder, out var t) ? Math.Max(0, t) : 0;
        public int Multiplicador => TurboAtivo ? 2 : 1;

        // ----- SETORES E CHEFE -----
        public Fase FaseAtual => Fases[Math.Clamp(indiceFase, 0, Fases.Count - 1)];
        public int NumeroSetor => indiceFase + 1;
        public double ProgressoSetor => Math.Clamp(pontosDaFase / PontosParaConcluirAgora, 0, 1);
        public bool ChefeAtivo { get; private set; }
        /// <summary>Energia do chefe, de 100 ate 0.</summary>
        public double EnergiaChefe { get; private set; }

        // ----- DIALOGO -----
        public IReadOnlyList<Fala> DialogoAtual => dialogo;
        public Fala? FalaAtual => indiceFala >= 0 && indiceFala < dialogo.Count ? dialogo[indiceFala] : null;
        public int IndiceFala => indiceFala;
        public double TempoFalaMs => tempoFalaMs;
        /// <summary>Aviso curto de M8 mostrado na troca de setor dos ciclos seguintes.</summary>
        public string AvisoDeCiclo { get; private set; } = "";

        // ----- INFORMACOES PARA A INTERFACE -----
        public bool Invencivel => tempoInvencivelMs > 0;
        public bool ControlesInvertidos => tempoMiragemMs > 0;
        public double TempoInvencivelMs => tempoInvencivelMs;
        public double TempoMiragemMs => tempoMiragemMs;
        public double TempoEfeitoMs => tempoEfeitoMs;
        public Elemento? ElementoAtingido { get; private set; }

        /// <summary>Vai de 1 (momento da batida) ate 0 (fim do efeito).</summary>
        public double IntensidadeExplosao => Estado == EstadoJogo.Colidindo
            ? Math.Clamp(tempoEfeitoMs / Config.DuracaoExplosaoMs, 0, 1)
            : 0;

        /// <summary>0 no comeco do setor, 1 no momento mais apertado (ou no chefe).</summary>
        public double Intensidade => ChefeAtivo ? 1 : ProgressoSetor;

        /// <summary>Disparado a cada evento sonoro. A interface decide como tocar.</summary>
        public event Action<EventoSom>? Som;

        // =====================================================================
        // DIFICULDADE POR CICLO
        // =====================================================================

        /// <summary>
        /// A cada volta completa pelos tres setores tudo fica mais rapido.
        /// O fator tem teto para o jogo nao virar algo impossivel.
        /// </summary>
        public double FatorCiclo => Math.Min(1 + (Ciclo - 1) * 0.11, 2.0);

        private double VelocidadeInicialAgora => FaseAtual.VelocidadeInicial * FatorCiclo;
        private double VelocidadeMaximaAgora => FaseAtual.VelocidadeMaxima * FatorCiclo;
        private double PontosParaConcluirAgora => FaseAtual.PontosParaConcluir * (1 + (Ciclo - 1) * 0.12);
        private double ChanceOndaDuplaAgora => Math.Min(0.9, FaseAtual.ChanceOndaDupla * (1 + (Ciclo - 1) * 0.15));

        // =====================================================================
        // CICLO DE VIDA
        // =====================================================================

        /// <summary>Prepara (ou reinicia) uma partida e volta para a tela inicial.</summary>
        public void IniciaJogo()
        {
            indiceFase = 0;
            Ciclo = 1;
            pontosDaFase = 0;
            PontosTempo = PontosObstaculos = PontosLagrimas = 0;
            Lagrimas = ObstaculosSuperados = PowerUpsPegos = 0;
            TempoJogoMs = 0;
            DistanciaPercorrida = 0;
            tempoEfeitoMs = tempoInvencivelMs = tempoMiragemMs = 0;
            ElementoAtingido = null;
            Vidas = Config.VidasIniciais;
            VelocidadeEstrada = VelocidadeInicialAgora * Config.Altura;

            poderes.Clear();
            TemEscudo = false;
            ChefeAtivo = false;
            EnergiaChefe = 0;
            Chefe = null;
            AvisoDeCiclo = "";

            dialogo.Clear();
            dialogosVistos.Clear();
            indiceFala = 0;
            tempoFalaMs = 0;

            Elementos.Clear();

            FaixaAlvo = Config.QtdFaixas / 2;
            Jogador = new Elemento
            {
                Tipo = TipoElemento.Jogador,
                Largura = Config.LarguraJogador,
                Altura = Config.AlturaJogador,
                Faixa = FaixaAlvo
            };
            Jogador.PosicaoX = PosicionaObjeto(FaixaAlvo);
            Jogador.PosicaoY = Config.Altura - Config.RecuoJogador - Jogador.Altura;

            faixasLivresAnteriores = Enumerable.Range(0, Config.QtdFaixas).ToList();
            referenciaOndaAnterior = null;
            ancoraAnterior = FaixaAlvo;
            repeticoesCorredor = 0;
            distanciaProximaOnda = Config.Altura * 0.75;

            Estado = EstadoJogo.Menu;
        }

        /// <summary>Sai da tela inicial. Comeca pela conversa de abertura.</summary>
        public void Comecar()
        {
            if (Estado != EstadoJogo.Menu) IniciaJogo();
            Som?.Invoke(EventoSom.Inicio);

            if (!AbreDialogoDoSetor()) Estado = EstadoJogo.Jogando;
        }

        /// <summary>Acao do ENTER/ESPACO: comeca, avanca a fala, despausa ou joga de novo.</summary>
        public void Confirmar()
        {
            switch (Estado)
            {
                case EstadoJogo.Menu:
                    Comecar();
                    break;
                case EstadoJogo.Dialogo:
                    AvancarFala();
                    break;
                case EstadoJogo.GameOver:
                    IniciaJogo();
                    Comecar();
                    break;
                case EstadoJogo.Pausado:
                    Estado = EstadoJogo.Jogando;
                    break;
            }
        }

        public void AlternarPausa()
        {
            if (Estado == EstadoJogo.Jogando) Estado = EstadoJogo.Pausado;
            else if (Estado == EstadoJogo.Pausado) Estado = EstadoJogo.Jogando;
        }

        // =====================================================================
        // DIALOGO
        // =====================================================================

        /// <summary>Abre a conversa do setor atual, se ela ainda nao foi vista.</summary>
        private bool AbreDialogoDoSetor(string chaveExtra = "")
        {
            var chave = chaveExtra != "" ? chaveExtra : $"setor{indiceFase}";
            var falas = chaveExtra == "Dialogos.AposYoutopia"
                ? Dialogos.AposYoutopia()
                : FaseAtual.Dialogo?.Invoke();

            if (falas is null || falas.Count == 0 || dialogosVistos.Contains(chave)) return false;

            dialogosVistos.Add(chave);
            dialogo.Clear();
            dialogo.AddRange(falas);
            indiceFala = 0;
            tempoFalaMs = 0;
            Estado = EstadoJogo.Dialogo;
            Som?.Invoke(EventoSom.Fala);
            return true;
        }

        /// <summary>Pula a conversa inteira e ja comeca a correr.</summary>
        public void PularDialogo()
        {
            if (Estado != EstadoJogo.Dialogo) return;

            dialogo.Clear();
            indiceFala = 0;
            tempoFalaMs = 0;
            Estado = EstadoJogo.Jogando;
        }

        /// <summary>Passa para a proxima fala (ou comeca a correr, se acabou).</summary>
        public void AvancarFala()
        {
            if (Estado != EstadoJogo.Dialogo) return;

            indiceFala++;
            tempoFalaMs = 0;

            if (indiceFala >= dialogo.Count)
            {
                dialogo.Clear();
                indiceFala = 0;
                Estado = EstadoJogo.Jogando;
            }
            else
            {
                Som?.Invoke(EventoSom.Fala);
            }
        }

        // =====================================================================
        // ATUALIZACAO (chamada a cada quadro, com o tempo real decorrido)
        // =====================================================================

        public void Atualizar(double deltaMs)
        {
            deltaMs = Math.Clamp(deltaMs, 0, 60);   // evita saltos depois de uma travada
            var dt = deltaMs / 1000.0;

            foreach (var e in Elementos) e.Animacao += deltaMs;
            if (Chefe is not null) Chefe.Animacao += deltaMs;

            if (Estado == EstadoJogo.Dialogo)
            {
                tempoFalaMs += deltaMs;
                if (tempoFalaMs > 3600) AvancarFala();   // passa sozinho se o jogador nao apertar nada
                return;
            }

            if (Estado == EstadoJogo.Colidindo)
            {
                tempoEfeitoMs -= deltaMs;
                if (tempoEfeitoMs <= 0) TerminaExplosao();
                return;
            }

            if (Estado == EstadoJogo.TrocandoSetor)
            {
                tempoEfeitoMs -= deltaMs;
                if (tempoEfeitoMs <= 0) Estado = EstadoJogo.Jogando;
                return;
            }

            if (Estado != EstadoJogo.Jogando) return;

            TempoJogoMs += deltaMs;
            if (tempoInvencivelMs > 0) tempoInvencivelMs -= deltaMs;
            if (tempoMiragemMs > 0) tempoMiragemMs -= deltaMs;
            AtualizaPoderes(deltaMs);

            Acelerar(FaseAtual.AceleracaoPorSegundo * FatorCiclo * Config.Altura * dt);
            DistanciaPercorrida += VelocidadeAtual * dt;

            // (a) PONTOS POR TEMPO
            var ganho = FaseAtual.PontosPorSegundo * dt * Multiplicador;
            PontosTempo += ganho;
            pontosDaFase += ganho;

            MovimentaJogador(dt);
            MovimentaObstaculos(dt);
            GeraElementos();
            AtualizaChefe(dt);
            VerificaColetas();
            VerificaColisao();
        }

        /// <summary>Velocidade com que a pista corre agora, ja com os power-ups.</summary>
        public double VelocidadeAtual
        {
            get
            {
                var v = VelocidadeEstrada;
                if (LentidaoAtiva) v *= 0.60;
                return v;
            }
        }

        /// <summary>
        /// Velocidade maxima que a pista pode atingir neste setor/ciclo. E com ela que o
        /// motor calcula o espacamento seguro - sempre pelo pior caso. Nenhum power-up
        /// acelera a pista, justamente para esse calculo continuar valendo.
        /// </summary>
        private double VelocidadeDePlanejamento => VelocidadeMaximaAgora * Config.Altura;

        /// <summary>Aumenta a velocidade da pista, respeitando o teto do setor.</summary>
        public void Acelerar(double incremento)
        {
            var maxima = VelocidadeMaximaAgora * Config.Altura;
            VelocidadeEstrada = Math.Min(maxima, VelocidadeEstrada + incremento);
        }

        private void AtualizaPoderes(double deltaMs)
        {
            foreach (var poder in poderes.Keys.ToList())
            {
                if (poderes[poder] > 0) poderes[poder] -= deltaMs;
            }
        }

        // =====================================================================
        // MOVIMENTO
        // =====================================================================

        private void MovimentaJogador(double dt)
        {
            var alvo = PosicionaObjeto(FaixaAlvo);
            var passo = Config.VelocidadeLateral * dt;
            var diferenca = alvo - Jogador.PosicaoX;

            if (Math.Abs(diferenca) <= passo)
            {
                Jogador.PosicaoX = alvo;
                Jogador.Faixa = FaixaAlvo;
            }
            else
            {
                Jogador.PosicaoX += Math.Sign(diferenca) * passo;
            }
        }

        /// <summary>Move os elementos da pista e conta os obstaculos que ficaram para tras.</summary>
        public void MovimentaObstaculos(double dt)
        {
            var velocidade = VelocidadeAtual;

            for (int i = Elementos.Count - 1; i >= 0; i--)
            {
                var e = Elementos[i];
                e.PosicaoY += velocidade * e.FatorVelocidade * dt;

                if (e.Tipo == TipoElemento.Lagrima && ImaAtivo) AtrairLagrima(e, dt);

                // (b) PONTOS POR OBSTACULO SUPERADO
                if (e.Ativo && !e.Contabilizado && e.Tipo == TipoElemento.Obstaculo
                    && e.PosicaoY > Jogador.PosicaoY + Jogador.Altura)
                {
                    e.Contabilizado = true;
                    ObstaculosSuperados++;
                    var pontos = e.Pontos * Multiplicador;
                    PontosObstaculos += pontos;
                    pontosDaFase += pontos;
                    Som?.Invoke(EventoSom.ObstaculoSuperado);
                }

                if (!e.Ativo || e.PosicaoY > Config.Altura) Elementos.RemoveAt(i);
            }
        }

        /// <summary>Com a Conexao ativa, as lagrimas proximas vem na direcao de Selene.</summary>
        private void AtrairLagrima(Elemento lagrima, double dt)
        {
            var raio = Config.Altura * 0.45;
            var dx = Jogador.CentroX - lagrima.CentroX;
            var dy = Jogador.CentroY - lagrima.CentroY;
            var distancia = Math.Sqrt(dx * dx + dy * dy);

            if (distancia > raio || distancia < 1) return;

            var passo = Config.VelocidadeLateral * 0.8 * dt;
            lagrima.PosicaoX += dx / distancia * passo;
            lagrima.PosicaoY += dy / distancia * passo * 0.5;
        }

        // =====================================================================
        // GERACAO DE ONDAS - AQUI MORA A GARANTIA DE PASSAGEM
        // =====================================================================

        /// <summary>
        /// O jogo nunca exige mais de UMA troca de faixa entre duas ondas: e isso que
        /// torna a passagem sempre possivel, e nao depende de reflexo sobre-humano.
        /// </summary>
        public const int FaixasPorOnda = 1;

        /// <summary>
        /// Vao minimo entre duas ondas para o jogador conseguir trocar de faixa a tempo:
        /// a distancia percorrida durante (uma troca de faixa + tempo de reacao), mais o
        /// espaco que a onda anterior precisa para sair da frente do corpo dele.
        /// </summary>
        public double VaoMinimoSeguro => VaoMinimoPara(FaixasPorOnda);

        private double VaoMinimoPara(int faixasATravessar)
        {
            var tempo = faixasATravessar * Config.TempoPorFaixa / Config.MargemSeguranca
                      + Config.TempoDeReacao;
            return VelocidadeDePlanejamento * tempo + FolgaDeCorpo;
        }

        /// <summary>Altura do maior obstaculo que o jogo pode criar.</summary>
        private int AlturaMaximaObstaculo => (int)Math.Ceiling(Config.AlturaJogador * 1.45);

        /// <summary>
        /// Distancia que a onda anterior ainda precisa percorrer, DEPOIS de chegar na linha
        /// do jogador, para liberar a passagem: a propria altura dela mais a altura do
        /// jogador. So quando isso acontece e que da para comecar a trocar de faixa.
        /// </summary>
        private int FolgaDeCorpo => Config.AlturaJogador + AlturaMaximaObstaculo;

        /// <summary>Altura usada como ponto de nascimento das ondas (fora da tela).</summary>
        private int AlturaDeReferencia => AlturaMaximaObstaculo + 4;

        /// <summary>Quantos segundos faltam para um elemento chegar na linha do jogador.</summary>
        private double TempoDeChegada(double posicaoY, double fator)
            => (Jogador.PosicaoY - posicaoY) / (VelocidadeDePlanejamento * fator);

        private void GeraElementos()
        {
            if (DistanciaPercorrida < distanciaProximaOnda) return;

            // dificuldade progressiva: a folga encolhe conforme o setor avanca, mas nunca
            // fica abaixo de 1,0 vez o vao minimo seguro - esse e o piso que garante a
            // passagem. A variacao sorteada so AUMENTA o vao, nunca diminui.
            var fase = FaseAtual;
            var folga = Lerp(fase.FolgaInicial, fase.FolgaFinal, Intensidade);
            var variacao = 1 + rnd.NextDouble() * 0.15;
            var vao = VaoMinimoSeguro * Math.Max(1.0, folga) * variacao;
            distanciaProximaOnda = DistanciaPercorrida + vao;

            GeraOnda(EscolhePadrao());
        }

        private PadraoOnda EscolhePadrao()
        {
            var fase = FaseAtual;

            // o corredor dura algumas ondas seguidas
            if (repeticoesCorredor > 0)
            {
                repeticoesCorredor--;
                return PadraoOnda.Corredor;
            }

            var padrao = fase.Padroes[rnd.Next(fase.Padroes.Length)];

            if (padrao == PadraoOnda.Corredor) repeticoesCorredor = rnd.Next(1, 3);

            // as ondas duplas (so uma passagem) ja aparecem no comeco do setor e
            // ficam mais frequentes conforme ele avanca
            if (padrao == PadraoOnda.Simples
                && rnd.NextDouble() < ChanceOndaDuplaAgora * (0.45 + 0.55 * Intensidade))
            {
                padrao = PadraoOnda.Dupla;
            }

            return padrao;
        }

        /// <summary>
        /// Monta uma onda de obstaculos deixando SEMPRE pelo menos uma faixa livre
        /// que o jogador consegue alcancar a tempo, saindo de qualquer faixa livre
        /// da onda anterior.
        /// </summary>
        private void GeraOnda(PadraoOnda padrao)
        {
            var fase = FaseAtual;

            // 1) onde a onda nasce (referencia = obstaculo de velocidade normal).
            //    E sempre a mesma altura, para o espacamento entre ondas ser exato.
            var yReferencia = (double)-AlturaDeReferencia;
            var chegadaNova = TempoDeChegada(yReferencia, 1.0);
            var chegadaAnterior = referenciaOndaAnterior is null
                ? 0
                : Math.Max(0, TempoDeChegada(referenciaOndaAnterior.PosicaoY, referenciaOndaAnterior.FatorVelocidade));

            // 2) tempo util para trocar de faixa: o que sobra entre a onda anterior
            //    liberar o corpo do jogador e a proxima chegar na frente dele
            var tempoDoCorpo = FolgaDeCorpo / VelocidadeDePlanejamento;
            var disponivel = Math.Max(0, chegadaNova - chegadaAnterior - tempoDoCorpo);
            var alcanceReal = (int)Math.Floor(disponivel * Config.MargemSeguranca / Config.TempoPorFaixa);

            if (alcanceReal < FaixasPorOnda)
            {
                // as ondas ficaram perto demais (troca de setor, volta de batida...):
                // adia esta onda em vez de criar uma situacao impossivel
                distanciaProximaOnda = DistanciaPercorrida + VaoMinimoSeguro * 0.5;
                return;
            }

            // 3) faixas alcancaveis a tempo SAINDO DE QUALQUER faixa livre da onda anterior
            //    (o jogador pode estar em qualquer uma delas, entao vale o pior caso)
            var candidatas = Enumerable.Range(0, Config.QtdFaixas)
                .Where(destino => faixasLivresAnteriores.All(origem => Math.Abs(destino - origem) <= FaixasPorOnda))
                .ToList();

            // rede de seguranca: se nao sobrou nenhuma opcao, fica com a faixa mais central
            if (candidatas.Count == 0 && faixasLivresAnteriores.Count > 0)
            {
                var melhor = Enumerable.Range(0, Config.QtdFaixas)
                    .OrderBy(destino => faixasLivresAnteriores.Max(origem => Math.Abs(destino - origem)))
                    .First();
                candidatas = [melhor];
            }
            if (candidatas.Count == 0) candidatas = [FaixaAlvo];

            // 4) a passagem garantida desta onda
            var ancora = EscolheAncora(padrao, candidatas);

            // 5) quantas faixas bloquear
            var bloquear = padrao == PadraoOnda.Simples ? 1 : Config.QtdFaixas - 1;
            bloquear = Math.Min(bloquear, Config.QtdFaixas - 1);

            var disponiveis = Enumerable.Range(0, Config.QtdFaixas).Where(f => f != ancora).ToList();
            var bloqueadas = new List<int>();
            while (bloqueadas.Count < bloquear && disponiveis.Count > 0)
            {
                var escolhida = disponiveis[rnd.Next(disponiveis.Count)];
                disponiveis.Remove(escolhida);
                bloqueadas.Add(escolhida);
            }

            // 6) cria os obstaculos, ajustando o Y de cada um para TODOS chegarem juntos
            Elemento? referencia = null;
            foreach (var faixa in bloqueadas)
            {
                var tipo = fase.Obstaculos[rnd.Next(fase.Obstaculos.Length)];
                var ob = Elemento.CriarObstaculo(tipo, Config);
                ob.Faixa = faixa;
                ob.Variacao = rnd.NextDouble();
                ob.PosicaoX = Config.CentroFaixa(faixa) - ob.Largura / 2.0;

                // y = Yjogador - fator * (Yjogador - yReferencia)  =>  mesma hora de chegada
                ob.PosicaoY = Jogador.PosicaoY - ob.FatorVelocidade * (Jogador.PosicaoY - yReferencia);

                Elementos.Add(ob);
                referencia ??= ob;
            }

            // 7) guarda o estado da onda para a proxima decisao
            faixasLivresAnteriores = Enumerable.Range(0, Config.QtdFaixas).Except(bloqueadas).ToList();
            referenciaOndaAnterior = referencia;
            ancoraAnterior = ancora;

            // 8) itens nas faixas livres
            GeraItens(faixasLivresAnteriores, yReferencia);
        }

        /// <summary>
        /// Escolhe a faixa que vai ficar livre nesta onda. Sempre dentro das candidatas
        /// (as que o jogador alcanca a tempo), mas preferindo uma DIFERENTE da que ele
        /// esta agora - senao bastaria ficar parado no meio da pista.
        /// </summary>
        private int EscolheAncora(PadraoOnda padrao, List<int> candidatas)
        {
            switch (padrao)
            {
                case PadraoOnda.Corredor:
                    return candidatas.Contains(ancoraAnterior)
                        ? ancoraAnterior
                        : candidatas.OrderBy(f => Math.Abs(f - ancoraAnterior)).First();

                case PadraoOnda.Zigue:
                    return candidatas.OrderByDescending(f => Math.Abs(f - ancoraAnterior)).First();

                default:
                    var longeDoJogador = candidatas.Where(f => f != FaixaAlvo).ToList();
                    if (longeDoJogador.Count > 0 && rnd.NextDouble() < 0.80)
                    {
                        return longeDoJogador[rnd.Next(longeDoJogador.Count)];
                    }
                    return candidatas[rnd.Next(candidatas.Count)];
            }
        }

        /// <summary>Lagrimas e Insights, sempre em faixas livres.</summary>
        private void GeraItens(List<int> faixasLivres, double yReferencia)
        {
            if (faixasLivres.Count == 0) return;
            var fase = FaseAtual;

            if (rnd.NextDouble() < fase.ChanceLagrima)
            {
                var faixa = faixasLivres[rnd.Next(faixasLivres.Count)];
                var quantas = rnd.Next(2, 5);
                var espaco = Config.AlturaLagrima * 1.9;

                for (int i = 0; i < quantas; i++)
                {
                    var lagrima = Elemento.CriarLagrima(Config);
                    lagrima.Faixa = faixa;
                    lagrima.PosicaoX = Config.CentroFaixa(faixa) - lagrima.Largura / 2.0;
                    lagrima.PosicaoY = yReferencia - i * espaco;
                    lagrima.Animacao = i * 120;
                    Elementos.Add(lagrima);
                }
            }

            if (rnd.NextDouble() < fase.ChancePowerUp)
            {
                var faixa = faixasLivres[rnd.Next(faixasLivres.Count)];
                var tipo = SorteiaPowerUp();
                var item = Elemento.CriarPowerUp(tipo, Config);
                item.Faixa = faixa;
                item.PosicaoX = Config.CentroFaixa(faixa) - item.Largura / 2.0;
                item.PosicaoY = yReferencia - Config.AlturaJogador * 1.2;
                Elementos.Add(item);
            }
        }

        private TipoPowerUp SorteiaPowerUp()
        {
            // a Empatia (escudo) aparece mais quando o jogador esta mal das vidas
            if (!TemEscudo && Vidas <= 1 && rnd.NextDouble() < 0.6) return TipoPowerUp.Empatia;

            var todos = Enum.GetValues<TipoPowerUp>();
            return todos[rnd.Next(todos.Length)];
        }

        /// <summary>Posicao X em que o jogador fica na faixa (0 = faixa da esquerda).</summary>
        public double PosicionaObjeto(int faixa) => Config.CentroFaixa(faixa) - Config.LarguraJogador / 2.0;

        // =====================================================================
        // COMANDOS DO JOGADOR
        // =====================================================================

        public void MoverEsquerda() => MoverParaFaixa(FaixaAlvo + (ControlesInvertidos ? 1 : -1));
        public void MoverDireita() => MoverParaFaixa(FaixaAlvo + (ControlesInvertidos ? -1 : 1));

        public void MoverParaFaixa(int faixa)
        {
            if (Estado != EstadoJogo.Jogando) return;

            faixa = Math.Clamp(faixa, 0, Config.QtdFaixas - 1);
            if (faixa == FaixaAlvo) return;

            FaixaAlvo = faixa;
            Som?.Invoke(EventoSom.TrocaFaixa);
        }

        // =====================================================================
        // COLETAS
        // =====================================================================

        private void VerificaColetas()
        {
            foreach (var item in Elementos)
            {
                if (!item.Ativo) continue;

                if (item.Tipo == TipoElemento.Lagrima && Jogador.Colide(item, 2))
                {
                    item.Ativo = false;
                    Lagrimas++;
                    var pontos = item.Pontos * Multiplicador;
                    PontosLagrimas += pontos;
                    pontosDaFase += pontos;
                    if (ChefeAtivo) EnergiaChefe -= 5;   // a lagrima machuca o chefe
                    Som?.Invoke(EventoSom.Lagrima);
                }
                else if (item.Tipo == TipoElemento.PowerUp && Jogador.Colide(item, 2))
                {
                    item.Ativo = false;
                    PowerUpsPegos++;
                    PontosLagrimas += item.Pontos;
                    pontosDaFase += item.Pontos;
                    AtivaPoder(item.PowerUp);
                    Som?.Invoke(EventoSom.PegouPowerUp);
                }
            }
        }

        private void AtivaPoder(TipoPowerUp poder)
        {
            switch (poder)
            {
                case TipoPowerUp.Empatia:
                    TemEscudo = true;
                    break;
                case TipoPowerUp.Proposito:
                    poderes[TipoPowerUp.Proposito] = Config.DuracaoTurboMs;
                    break;
                case TipoPowerUp.Conexao:
                    poderes[TipoPowerUp.Conexao] = Config.DuracaoImaMs;
                    break;
                case TipoPowerUp.Visao:
                    poderes[TipoPowerUp.Visao] = Config.DuracaoLentidaoMs;
                    break;
            }
        }

        // =====================================================================
        // COLISAO E EFEITO DA BATIDA
        // =====================================================================

        /// <summary>Procura sobreposicao entre o jogador e algum obstaculo.</summary>
        public bool ChecarColisao()
        {
            foreach (var ob in Elementos)
            {
                if (ob.Tipo == TipoElemento.Obstaculo && ob.Ativo
                    && Jogador.Colide(ob, Config.FolgaColisao))
                {
                    return true;
                }
            }
            return false;
        }

        private void VerificaColisao()
        {
            foreach (var ob in Elementos)
            {
                if (ob.Tipo != TipoElemento.Obstaculo || !ob.Ativo) continue;
                if (!Jogador.Colide(ob, Config.FolgaColisao)) continue;

                // a miragem nao machuca: inverte os controles por alguns segundos
                if (ob.Obstaculo == TipoObstaculo.Miragem)
                {
                    ob.Ativo = false;
                    tempoMiragemMs = Config.DuracaoMiragemMs;
                    Som?.Invoke(EventoSom.Miragem);
                    continue;
                }

                if (Invencivel) continue;

                if (TemEscudo)
                {
                    TemEscudo = false;
                    ob.Ativo = false;
                    tempoInvencivelMs = Config.DuracaoInvencivelMs * 0.6;
                    Som?.Invoke(EventoSom.EscudoQuebrado);
                    continue;
                }

                Bater(ob);
                return;
            }
        }

        private void Bater(Elemento ob)
        {
            ob.Ativo = false;
            ElementoAtingido = ob;
            Vidas--;
            Estado = EstadoJogo.Colidindo;
            tempoEfeitoMs = Config.DuracaoExplosaoMs;
            poderes.Clear();
            Som?.Invoke(EventoSom.Colisao);
        }

        private void TerminaExplosao()
        {
            tempoEfeitoMs = 0;

            if (Vidas <= 0)
            {
                Estado = EstadoJogo.GameOver;
                AtualizaRecorde();
                Som?.Invoke(EventoSom.GameOver);
                return;
            }

            // limpa a area em volta do jogador para nao bater de novo na sequencia
            Elementos.RemoveAll(e => e.Tipo == TipoElemento.Obstaculo
                                  && e.PosicaoY + e.Altura > Jogador.PosicaoY - Config.AlturaJogador * 2);

            faixasLivresAnteriores = Enumerable.Range(0, Config.QtdFaixas).ToList();
            referenciaOndaAnterior = null;
            distanciaProximaOnda = DistanciaPercorrida + VaoMinimoSeguro;

            tempoInvencivelMs = Config.DuracaoInvencivelMs;
            tempoMiragemMs = 0;
            VelocidadeEstrada = VelocidadeInicialAgora * Config.Altura;
            Estado = EstadoJogo.Jogando;
        }

        // =====================================================================
        // CHEFE E SETORES
        // =====================================================================

        private void AtualizaChefe(double dt)
        {
            if (!ChefeAtivo)
            {
                if (pontosDaFase >= PontosParaConcluirAgora) ChamaChefe();
                return;
            }

            // o chefe perde energia com o tempo (e leva dano a cada lagrima pega)
            EnergiaChefe -= 100.0 / (FaseAtual.TempoChefeMs / 1000.0) * dt;

            if (Chefe is not null)
            {
                var passeio = Math.Sin(Chefe.Animacao / 700.0) * Config.LarguraFaixa * 0.8;
                Chefe.PosicaoX = Config.Largura / 2.0 - Chefe.Largura / 2.0 + passeio;
            }

            if (EnergiaChefe <= 0) DerrotaChefe();
        }

        private void ChamaChefe()
        {
            ChefeAtivo = true;
            EnergiaChefe = 100;

            Chefe = new Elemento
            {
                Tipo = TipoElemento.Chefe,
                Largura = (int)(Config.LarguraJogador * 2.4),
                Altura = (int)(Config.AlturaJogador * 2.4)
            };
            Chefe.PosicaoX = Config.Largura / 2.0 - Chefe.Largura / 2.0;
            Chefe.PosicaoY = Config.Altura * 0.15;   // abaixo do painel da interface

            Som?.Invoke(EventoSom.ChefeEntrou);
        }

        private void DerrotaChefe()
        {
            ChefeAtivo = false;
            EnergiaChefe = 0;
            Chefe = null;
            Som?.Invoke(EventoSom.ChefeDerrotado);

            var fechouCiclo = indiceFase >= Fases.Count - 1;
            indiceFase = fechouCiclo ? 0 : indiceFase + 1;
            if (fechouCiclo) Ciclo++;

            pontosDaFase = 0;
            Elementos.Clear();
            faixasLivresAnteriores = Enumerable.Range(0, Config.QtdFaixas).ToList();
            referenciaOndaAnterior = null;
            repeticoesCorredor = 0;
            VelocidadeEstrada = VelocidadeInicialAgora * Config.Altura;
            distanciaProximaOnda = DistanciaPercorrida + Config.Altura;
            tempoInvencivelMs = Config.DuracaoInvencivelMs;
            tempoMiragemMs = 0;
            // vida de bonus so quando fecha uma volta inteira pelos tres setores
            if (fechouCiclo) Vidas = Math.Min(Config.VidasMaximas, Vidas + 1);

            // E.V.E. revela o ciclo quando o jogador fecha a primeira volta
            if (fechouCiclo && AbreDialogoDoSetor("Dialogos.AposYoutopia"))
            {
                Som?.Invoke(EventoSom.NovoCiclo);
                return;
            }

            // conversa de entrada do setor (so na primeira vez que ele aparece)
            if (AbreDialogoDoSetor())
            {
                Som?.Invoke(EventoSom.NovoSetor);
                return;
            }

            // nos ciclos seguintes, so um aviso rapido
            var avisos = Dialogos.AvisosDeCiclo();
            AvisoDeCiclo = avisos[rnd.Next(avisos.Length)];
            Estado = EstadoJogo.TrocandoSetor;
            tempoEfeitoMs = Config.DuracaoTrocaSetorMs;
            Som?.Invoke(fechouCiclo ? EventoSom.NovoCiclo : EventoSom.NovoSetor);
        }

        public bool VerificaFimJogo() => Estado == EstadoJogo.GameOver;

        private void AtualizaRecorde()
        {
            if (Pontuacao > MelhorPontuacao) MelhorPontuacao = Pontuacao;
        }

        private static double Lerp(double de, double ate, double fracao)
            => de + (ate - de) * Math.Clamp(fracao, 0, 1);
    }
}
