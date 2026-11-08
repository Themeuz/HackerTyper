# HackerTyper

Fica na bandeja do sistema. **Ctrl + Alt + P** abre um terminal transparente em tela cheia, no estilo Matrix.
Cada tecla que você aperta "digita" de 3 a 5 caracteres de código real.

## Rodar
- `HackerTyper.exe` (ou `HackerTyper.exe caminho\do\arquivo.c` para já abrir com um arquivo)
- Recompilar: `powershell -ExecutionPolicy Bypass -File build.ps1`

## Teclas (dentro do terminal)
| Tecla | Ação |
|---|---|
| qualquer tecla | digita código (3-5 chars por tecla, ~400 WPM) |
| F10 | painel de comandos de TI: digite para buscar, ↑↓ para navegar, Enter ou duplo clique copia |
| F12 | trocar tema (Matrix verde, âmbar, azul, vermelho) |
| F2 / F8 | modo automático / velocidade (400 WPM → LUDICROUS) |
| F5 | modo compilação: barra trava em 99% e dá erro fatal |
| F3 / F4 | ACESSO PERMITIDO / ACESSO NEGADO |
| F6 / F7 | chuva Matrix / nível de transparência |
| F9 | limpar tela |
| F11 | tela cheia / janela (arraste para mover) |
| Ctrl+O ou arrastar arquivo | carregar código-fonte ou log |
| Esc | esconder |

O código padrão vem da pasta `exemplos/` (coloque lá quantos arquivos quiser, por exemplo um log de build do kernel).
A lista de comandos de TI fica em `COMANDOS-TI.md`. Edite à vontade: o painel F10 relê o arquivo toda vez que abre.
O último arquivo carregado é lembrado. Menu da bandeja: carregar arquivo, modo automático, iniciar com o Windows, sair.
