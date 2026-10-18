namespace JogoCorrida
{
    /// <summary>
    /// Eventos sonoros disparados pelo motor (evento <see cref="Jogo.Som"/>).
    /// Quem escuta decide como tocar.
    /// </summary>
    public enum EventoSom
    {
        Inicio,
        TrocaFaixa,
        Lagrima,
        ObstaculoSuperado,
        Colisao,
        EscudoQuebrado,
        PegouPowerUp,
        Miragem,
        ChefeEntrou,
        ChefeDerrotado,
        NovoSetor,
        NovoCiclo,
        Fala,
        GameOver
    }
}
