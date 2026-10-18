namespace JogoCorrida
{
    /// <summary>
    /// Um setor da corrida. O jogo tem tres e fica repetindo eles para sempre,
    /// cada volta um pouco mais rapida (ver <see cref="Jogo.Ciclo"/>).
    ///
    /// Os lugares, os personagens e a historia vem do universo do album
    /// POST HUMAN: NeX GEn (ver Assets/CREDITOS.txt).
    /// </summary>
    public class Fase
    {
        public int Numero { get; set; }
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";

        /// <summary>Velocidade inicial da pista, em "telas por segundo".</summary>
        public double VelocidadeInicial { get; set; } = 0.42;
        public double VelocidadeMaxima { get; set; } = 0.62;
        /// <summary>Quanto a velocidade cresce por segundo dentro do setor.</summary>
        public double AceleracaoPorSegundo { get; set; } = 0.012;

        /// <summary>Pontos ganhos por segundo de sobrevivencia.</summary>
        public double PontosPorSegundo { get; set; } = 12;
        /// <summary>Pontos que precisam ser feitos DENTRO do setor para chamar o chefe.</summary>
        public int PontosParaConcluir { get; set; } = 340;

        // ---------------------------------------------------------------------
        // DIFICULDADE PROGRESSIVA
        // Multiplicadores aplicados sobre o VAO MINIMO SEGURO (aquele que ainda
        // permite trocar de faixa a tempo). Comecam folgados e vao apertando.
        // Nunca descem abaixo de 1,0 - esse e o piso que garante a passagem.
        // ---------------------------------------------------------------------
        public double FolgaInicial { get; set; } = 1.7;
        public double FolgaFinal { get; set; } = 1.1;

        public double ChanceLagrima { get; set; } = 0.80;
        public double ChancePowerUp { get; set; } = 0.16;
        /// <summary>Chance de a onda bloquear duas faixas em vez de uma.</summary>
        public double ChanceOndaDupla { get; set; } = 0.35;

        public TipoObstaculo[] Obstaculos { get; set; } = [TipoObstaculo.Vulto, TipoObstaculo.Barreira];
        public PadraoOnda[] Padroes { get; set; } = [PadraoOnda.Simples];

        /// <summary>Sprite do inimigo comum neste setor (agente, neosage, angeldust...).</summary>
        public string SpriteVulto { get; set; } = "agente";

        // ----- CHEFE -----
        public string NomeChefe { get; set; } = "";
        public string SpriteChefe { get; set; } = "";
        public string FraseChefe { get; set; } = "";
        public double TempoChefeMs { get; set; } = 14000;

        // ----- VISUAL (0xAARRGGBB) -----
        public uint CorFundo { get; set; } = 0xFF0A0710;
        public uint CorPista { get; set; } = 0xFF14101C;
        public uint CorNeon { get; set; } = 0xFFFF2D7A;
        public uint CorNeon2 { get; set; } = 0xFF22E3FF;

        /// <summary>Nome do arquivo de musica (dentro de Assets/Audio).</summary>
        public string Musica { get; set; } = "";

        /// <summary>Conversa que roda antes do setor (so na primeira volta).</summary>
        public Func<List<Fala>>? Dialogo { get; set; }

        /// <summary>Os tres setores do jogo, na ordem em que se repetem.</summary>
        public static List<Fase> FasesPadrao() =>
        [
            new Fase
            {
                Numero = 1,
                Nome = "NEW ELYSIUM",
                Descricao = "O suburbio da Nex Gen. Eles sabem que voce e imune.",
                VelocidadeInicial = 0.42,
                VelocidadeMaxima = 0.66,
                AceleracaoPorSegundo = 0.014,
                PontosPorSegundo = 12,
                PontosParaConcluir = 340,
                FolgaInicial = 1.70,
                FolgaFinal = 1.15,
                ChanceLagrima = 0.80,
                ChancePowerUp = 0.16,
                ChanceOndaDupla = 0.35,
                Obstaculos = [TipoObstaculo.Vulto, TipoObstaculo.Barreira, TipoObstaculo.Vulto],
                Padroes = [PadraoOnda.Simples, PadraoOnda.Simples, PadraoOnda.Corredor],
                SpriteVulto = "agente",
                NomeChefe = "VICTOR CARMINE",
                SpriteChefe = "carmine",
                FraseChefe = "o dono da Nex Gen veio te buscar",
                TempoChefeMs = 13000,
                CorFundo = 0xFF0A0711,
                CorPista = 0xFF15101F,
                CorNeon = 0xFFFF2D7A,
                CorNeon2 = 0xFF22E3FF,
                Musica = "invaderloop.mp3",
                Dialogo = Dialogos.Abertura
            },
            new Fase
            {
                Numero = 2,
                Nome = "OCTOBER PALACE",
                Descricao = "Novosovia. O que sobrou do culto Mantra.",
                VelocidadeInicial = 0.54,
                VelocidadeMaxima = 0.80,
                AceleracaoPorSegundo = 0.018,
                PontosPorSegundo = 16,
                PontosParaConcluir = 500,
                FolgaInicial = 1.55,
                FolgaFinal = 1.08,
                ChanceLagrima = 0.82,
                ChancePowerUp = 0.18,
                ChanceOndaDupla = 0.60,
                Obstaculos =
                [
                    TipoObstaculo.Vulto, TipoObstaculo.Barreira,
                    TipoObstaculo.Executor, TipoObstaculo.Miragem
                ],
                Padroes = [PadraoOnda.Simples, PadraoOnda.Dupla, PadraoOnda.Zigue, PadraoOnda.Corredor],
                SpriteVulto = "neosage",
                NomeChefe = "SKYS",
                SpriteChefe = "skys",
                FraseChefe = "o fundador do culto quer a sua chave",
                TempoChefeMs = 15000,
                CorFundo = 0xFF04100C,
                CorPista = 0xFF0D1A15,
                CorNeon = 0xFFB6FF3C,
                CorNeon2 = 0xFFFF2D7A,
                Musica = "bilwe.mp3",
                Dialogo = Dialogos.AposNewElysium
            },
            new Fase
            {
                Numero = 3,
                Nome = "YOUTOPIA",
                Descricao = "Nao e um lugar: e um estado. E ele se repete.",
                VelocidadeInicial = 0.70,
                VelocidadeMaxima = 0.96,
                AceleracaoPorSegundo = 0.022,
                PontosPorSegundo = 20,
                PontosParaConcluir = 700,
                FolgaInicial = 1.40,
                FolgaFinal = 1.00,
                ChanceLagrima = 0.85,
                ChancePowerUp = 0.22,
                ChanceOndaDupla = 0.80,
                Obstaculos =
                [
                    TipoObstaculo.Vulto, TipoObstaculo.Barreira, TipoObstaculo.Executor,
                    TipoObstaculo.Miragem, TipoObstaculo.Drone, TipoObstaculo.Drone
                ],
                Padroes = [PadraoOnda.Dupla, PadraoOnda.Zigue, PadraoOnda.Corredor, PadraoOnda.Simples],
                SpriteVulto = "angeldust",
                NomeChefe = "E.V.E.",
                SpriteChefe = "eve",
                FraseChefe = "a IA que inventou este lugar",
                TempoChefeMs = 17000,
                CorFundo = 0xFF0A0614,
                CorPista = 0xFF161022,
                CorNeon = 0xFF22E3FF,
                CorNeon2 = 0xFFFF2D7A,
                Musica = "hardwar.mp3",
                Dialogo = Dialogos.AposOctoberPalace
            }
        ];
    }

    /// <summary>Formato da onda de obstaculos - base da dificuldade progressiva.</summary>
    public enum PadraoOnda
    {
        /// <summary>Uma faixa bloqueada, sorteada.</summary>
        Simples,
        /// <summary>Duas faixas bloqueadas (sempre sobra uma alcancavel).</summary>
        Dupla,
        /// <summary>Repete a mesma passagem algumas vezes: exige precisao.</summary>
        Corredor,
        /// <summary>A passagem alterna de lado a cada onda.</summary>
        Zigue
    }
}
