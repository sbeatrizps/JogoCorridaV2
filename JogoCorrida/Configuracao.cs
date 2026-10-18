namespace JogoCorrida
{
    /// <summary>
    /// Medidas da area de jogo. Como o motor e usado tanto pelo WinForms (pixels)
    /// quanto pelo Console (caracteres), todas as medidas ficam aqui e cada
    /// interface informa a sua propria escala.
    /// </summary>
    public class Configuracao
    {
        public int Largura { get; set; } = 480;
        public int Altura { get; set; } = 640;

        /// <summary>Faixa de acostamento em cada lado da pista.</summary>
        public int MargemPista { get; set; } = 40;
        public int QtdFaixas { get; set; } = 3;

        public int LarguraJogador { get; set; } = 46;
        public int AlturaJogador { get; set; } = 54;
        public int LarguraLagrima { get; set; } = 26;
        public int AlturaLagrima { get; set; } = 26;
        public int LarguraPowerUp { get; set; } = 34;
        public int AlturaPowerUp { get; set; } = 34;

        /// <summary>Distancia do jogador ate a base da tela.</summary>
        public int RecuoJogador { get; set; } = 110;

        /// <summary>Velocidade da troca de faixa, na mesma unidade por segundo.</summary>
        public double VelocidadeLateral { get; set; } = 700;

        /// <summary>Folga aplicada na caixa de colisao (deixa o jogo menos injusto).</summary>
        public double FolgaColisao { get; set; } = 7;

        // ---------------------------------------------------------------------
        // GARANTIA DE PASSAGEM
        // ---------------------------------------------------------------------

        /// <summary>
        /// Fracao do tempo disponivel que o motor considera utilizavel ao decidir
        /// se o jogador consegue chegar a uma faixa livre. Menor = mais folga.
        /// O resto cobre o tempo de reacao humano e a aceleracao da pista.
        /// </summary>
        public double MargemSeguranca { get; set; } = 0.7;

        /// <summary>Tempo de reacao somado ao vao minimo entre ondas (segundos).</summary>
        public double TempoDeReacao { get; set; } = 0.26;

        public double DuracaoExplosaoMs { get; set; } = 900;
        public double DuracaoInvencivelMs { get; set; } = 1800;
        public double DuracaoTrocaSetorMs { get; set; } = 2600;
        public double DuracaoMiragemMs { get; set; } = 2200;

        public int VidasIniciais { get; set; } = 2;
        public int VidasMaximas { get; set; } = 3;
        public int PontosPorLagrima { get; set; } = 50;
        public int PontosPorPowerUp { get; set; } = 25;

        // Duracao de cada Insight (ms)
        public double DuracaoTurboMs { get; set; } = 7000;
        public double DuracaoImaMs { get; set; } = 8000;
        public double DuracaoLentidaoMs { get; set; } = 5000;

        public int LarguraPista => Largura - 2 * MargemPista;
        public int LarguraFaixa => LarguraPista / QtdFaixas;

        /// <summary>Tempo, em segundos, para o jogador atravessar uma faixa.</summary>
        public double TempoPorFaixa => LarguraFaixa / VelocidadeLateral;

        /// <summary>Centro horizontal da faixa informada (0 = faixa da esquerda).</summary>
        public double CentroFaixa(int faixa)
        {
            faixa = Math.Clamp(faixa, 0, QtdFaixas - 1);
            return MargemPista + LarguraFaixa * faixa + LarguraFaixa / 2.0;
        }
    }
}
