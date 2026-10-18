# POST HUMAN RUN

Jogo de corrida infinita em C# / .NET 10, feito como trabalho do IFSP.

É um **fan game** do álbum *POST HUMAN: NeX GEn*, do Bring Me the Horizon:
você é **Selene (NYX-01)**, a única imune ao vírus VSPR-37, correndo da **Nex Gen**
atrás de **Sof**. **M8**, o navegador multidimensional, guia a fuga.

> Arte e diálogos são originais, feitos para este trabalho. Nenhuma arte oficial,
> logotipo, letra ou gravação da banda foi usada — detalhes em
> [Assets/CREDITOS.txt](JogoCorridaWinFormsApp/Assets/CREDITOS.txt).

---

## Como jogar

| Tecla | Ação |
|---|---|
| ← → (ou A / D) | mudar de faixa |
| ENTER / ESPAÇO | começar, passar a fala, jogar de novo |
| P | pausar |
| M | ligar/desligar som e trilha |
| F2 | modo demonstração (o jogo joga sozinho) |
| R | ver o ranking (no menu ou no fim de jogo) |
| ESC | pular a conversa / pausar / sair |

**O jogo não tem fim.** Os três setores se repetem em ciclos, e cada volta é mais
rápida — como o joguinho do dinossauro. Você começa com **2 vidas** (máximo de 3, e
só ganha uma de bônus ao fechar uma volta inteira pelos três setores).

Uma partida dura mais ou menos **1 a 2 minutos**: quem fica parado perde em 18 segundos.

### Como ler a tela

O código de cores é a regra, e o jogo também avisa com um rótulo na primeira vez
que cada coisa aparece:

| Cor | Significa |
|---|---|
| 🔵 **ciano** | você (Selene) e o que dá para pegar |
| 🔴 **rosa** | perigo: encostou, perdeu vida |
| 🟡 **dourado** | poder temporário |

Todo elemento tem uma **mancha de luz no chão** com a cor da sua categoria — dá para
reagir antes mesmo de reconhecer o desenho.

### Os setores e os chefes

| Setor | Lugar | Chefe |
|---|---|---|
| 1 | **New Elysium** — o subúrbio da Nex Gen | **Victor Carmine** |
| 2 | **October Palace** — Novosovia, o que sobrou do culto Mantra | **Skys** |
| 3 | **Youtopia** — não é um lugar, é um estado | **E.V.E.** |

Nas três primeiras transições entra um **diálogo** com os personagens. No fim do
setor 3, E.V.E. revela que Youtopia é um loop — e o jogo recomeça no ciclo 2,
mais rápido. A história explica a mecânica.

### Itens

- **Lágrima do Guardião** (ciano): +50 pontos. Durante o chefe, também tira energia dele.
- Os **quatro Insights** de Youtopia, cada um com o poder que combina com o que significa:

| Insight | Arcanjo | No jogo |
|---|---|---|
| Empatia | Gabriel | escudo: segura uma batida |
| Propósito | Michael | pontos em dobro |
| Conexão | Raphael | ímã: atrai as lágrimas |
| Visão | Uriel | deixa a pista mais lenta |

## Ranking

Ao perder, se a pontuação entrar entre as 10 melhores, o jogo pede o **nome do
jogador** e grava a corrida em:

```
ranking.csv        <- na raiz do projeto, do lado do README
```

O arquivo fica **dentro do repositório de propósito**: assim o ranking entra no git
junto com o jogo. Depois do campeonato, basta `git add ranking.csv` e commitar que as
pontuações viajam com o código.

```
# POST HUMAN RUN - as 10 melhores corridas
# nome;pontos;ciclo;setor;data
CONCURSO;2036;1;2;2026-10-18 17:44
```

Abre no Bloco de Notas ou no Excel. As linhas que começam com `#` são comentário.
Para zerar o ranking, apague as linhas de pontuação e deixe só os comentários.

O jogo acha a raiz sozinho, subindo as pastas a partir do executável até encontrar a
solução ou o `.git`. Se alguém rodar o jogo solto (pasta publicada, pendrive), o
`ranking.csv` é criado do lado do `.exe`.

> Se duas pessoas jogarem em cópias diferentes do repositório e as duas commitarem,
> o git vai acusar conflito no `ranking.csv` — é só juntar as linhas das duas e
> manter as 10 maiores.

Os três primeiros também ficam no menu, e o recorde do alto da tela vem dali.
**R** abre o ranking no menu e na tela de fim de jogo.

---

## Estrutura

```
JogoCorrida/              -> o motor do jogo (regras). Não desenha nada.
  Jogo.cs                    regras, ondas, pontuação, ciclos, chefes
  Elemento.cs                qualquer coisa na pista
  Fase.cs                    os 3 setores: velocidade, inimigos, chefe, paleta, trilha
  Dialogo.cs                 as falas das transições
  Configuracao.cs            medidas da área de jogo (serve para WinForms e Console)

JogoCorridaWinFormsApp/   -> a versão gráfica
  FormJogo.cs                desenho da cena, HUD, diálogos e telas
  PixelArt.cs                TODOS os personagens, desenhados pixel a pixel
  Tutorial.cs                os rótulos que ensinam ("DESVIE!", "PEGUE +50")
  Tema.cs                    paleta neon e ferramentas de desenho
  Efeitos.cs                 partículas da explosão e textos flutuantes
  Sons.cs                    efeitos sonoros gerados por programação
  Musica.cs                  trilha em MP3 (uma por setor)
  Demo.cs                    modo demonstração
  Ranking.cs                 top 10 gravado em arquivo (nome, pontos, ciclo, data)
  Assets/                    músicas (CC0) + CREDITOS.txt

JogoCorridaConsoleApp/    -> a mesma partida em modo texto, com o mesmo motor
```

As regras não sabem desenhar e o desenho não sabe as regras — por isso o mesmo
`Jogo.cs` roda igual na janela e no terminal.

### Mexer nos desenhos

Cada sprite em `PixelArt.cs` é um desenho em texto, uma letra por pixel:

```
.......KK.......     K = contorno
......KRRK......     R = rosa (perigo)
.....KRRRRK.....     C = ciano, Y = dourado, W = branco...
```

Para mudar um personagem, é só trocar as letras. A paleta inteira está no topo
do arquivo.

---

## A garantia de passagem

O motor **calcula** o espaço entre as ondas em vez de sortear, para nunca existir
uma situação impossível:

1. o jogo **nunca exige mais de uma troca de faixa por onda** (`Jogo.FaixasPorOnda`);
2. o vão mínimo entre duas ondas é a distância percorrida durante *uma troca de faixa
   ÷ margem de segurança* + *tempo de reação* + *(corpo da Selene + o maior inimigo)* —
   esse último termo é o espaço que a onda anterior precisa para sair da frente;
3. a faixa livre da próxima onda é escolhida entre as alcançáveis **saindo de qualquer**
   faixa livre da onda anterior (pior caso, não caso médio);
4. a dificuldade aperta a folga de 1,7× até **1,0×** o vão mínimo, nunca menos;
5. se as ondas ficarem perto demais (volta de uma batida, troca de setor), a onda é
   **adiada** em vez de nascer impossível.

Isso vale também nos ciclos mais rápidos: o cálculo usa a velocidade máxima do ciclo
atual. Verificado por simulação — um robô que só desvia corre **até o ciclo 9
(velocidade ×1,9) sem bater uma vez e sem usar escudo**.

---

## Como rodar

```bash
dotnet run --project JogoCorridaWinFormsApp
```

Modo demonstração (para deixar rodando numa apresentação):

```bash
dotnet run --project JogoCorridaWinFormsApp -- --demo
```

Versão em terminal:

```bash
dotnet run --project JogoCorridaConsoleApp
```
