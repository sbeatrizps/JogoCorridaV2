namespace JogoCorrida
{
    /// <summary>Uma fala de um personagem nas transicoes entre setores.</summary>
    public class Fala
    {
        /// <summary>Nome que aparece na caixa de dialogo.</summary>
        public string Personagem { get; set; } = "";

        /// <summary>Qual retrato desenhar (nome do sprite).</summary>
        public string Retrato { get; set; } = "";

        public string Texto { get; set; } = "";

        /// <summary>Cor do nome e da borda (0xAARRGGBB).</summary>
        public uint Cor { get; set; } = 0xFF22E3FF;

        public Fala() { }

        public Fala(string personagem, string retrato, string texto, uint cor)
        {
            Personagem = personagem;
            Retrato = retrato;
            Texto = texto;
            Cor = cor;
        }
    }

    /// <summary>
    /// As conversas que ligam os setores, contando a historia de Selene (NYX-01)
    /// atras de Sof. Texto escrito para este jogo, baseado no universo do album
    /// POST HUMAN: NeX GEn (ver Assets/CREDITOS.txt).
    /// </summary>
    public static class Dialogos
    {
        private const uint Ciano = 0xFF22E3FF;
        private const uint Rosa = 0xFFFF2D7A;
        private const uint Verde = 0xFFB6FF3C;
        private const uint Branco = 0xFFF0F4FF;

        /// <summary>Abertura: M8 acorda Selene em New Elysium.</summary>
        public static List<Fala> Abertura() =>
        [
            new("M8", "m8", "Acorda, NYX-01.", Ciano),
            new("SELENE", "selene", "Esse nome nao e meu.", Branco),
            new("M8", "m8", "Para a Nex Gen e. Voce e a unica que o VSPR-37 nao levou.", Ciano),
            new("M8", "m8", "E por isso que eles nunca param de correr atras de voce.", Ciano),
            new("SELENE", "selene", "Onde esta Sof?", Branco),
            new("M8", "m8", "Do outro lado. Corre - e nao deixa eles te tocarem.", Ciano)
        ];

        /// <summary>Depois de escapar de Victor Carmine, rumo a Novosovia.</summary>
        public static List<Fala> AposNewElysium() =>
        [
            new("VICTOR CARMINE", "carmine", "Voce nao esta fugindo, Selene. Esta seguindo o roteiro.", Rosa),
            new("M8", "m8", "Nao escuta. Saimos de New Elysium.", Ciano),
            new("M8", "m8", "Isto aqui e Novosovia. O que sobrou do culto Mantra ainda anda por aqui.", Ciano),
            new("SELENE", "selene", "O que eles querem?", Branco),
            new("M8", "m8", "Iluminacao atraves da dor. Eles procuram a porta. Voce e a chave.", Ciano)
        ];

        /// <summary>Depois de Skys, entrando em Youtopia.</summary>
        public static List<Fala> AposOctoberPalace() =>
        [
            new("SKYS", "skys", "Eu abri a porta com sofrimento. Voce quer atravessar de graca?", Verde),
            new("M8", "m8", "Ele perdeu o culto inteiro tentando. Continua.", Ciano),
            new("M8", "m8", "O que vem agora nao e um lugar. E um estado: Youtopia.", Ciano),
            new("SELENE", "selene", "Sof esta ai dentro?", Branco),
            new("M8", "m8", "Alguma versao dela esta. Eu nao sei mais qual e a real.", Ciano)
        ];

        /// <summary>E.V.E. revela o ciclo - e o jogo recomeca, mais dificil.</summary>
        public static List<Fala> AposYoutopia() =>
        [
            new("E.V.E.", "eve", "Bem-vinda de volta, Selene.", Rosa),
            new("E.V.E.", "eve", "Este e o ciclo 2. Voce ja correu os tres setores antes.", Rosa),
            new("E.V.E.", "eve", "Youtopia nao tem saida. Tem repeticao.", Rosa),
            new("M8", "m8", "...o mapa reiniciou. De novo.", Ciano),
            new("SELENE", "selene", "Entao eu corro de novo. Ate achar ela.", Branco)
        ];

        /// <summary>Comentarios curtos de M8 nos ciclos seguintes (sem parar o jogo).</summary>
        public static string[] AvisosDeCiclo() =>
        [
            "o ciclo acelerou. segura.",
            "mesma pista, menos tempo para pensar.",
            "eles aprenderam com a sua ultima corrida.",
            "a simulacao esta mais apertada agora.",
            "Sof ainda esta la na frente. continua."
        ];
    }
}
