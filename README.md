# scripts_bkp_seriais

Utilitário para Windows que identifica o produto, a edição, a versão e a compilação do sistema, consulta o estado de ativação e salva um relatório `.txt` na Área de Trabalho. Não extrai, exibe nem salva chaves de produto.

O relatório não contém a chave do produto. Para reinstalar o Windows, consulte **Configurações > Sistema > Ativação** e use a licença digital associada ao dispositivo ou à conta Microsoft. Se o Windows veio pré-instalado, consulte o fabricante do computador ou o Suporte Microsoft para opções oficiais de recuperação.

## Executar

O executável independente está em `publish\WindowsBackupInfo.exe`. Ao abri-lo, o programa exibe as informações, grava o relatório na Área de Trabalho e aguarda Enter antes de fechar.

## Gerar novamente o executável

Com o SDK do .NET 10 instalado, execute na pasta do projeto:

```powershell
dotnet publish -c Release -o .\publish
```

O executável independente será criado em `publish\WindowsBackupInfo.exe`.
