# Setup Rápido - Integração Canva + TVPlayer

## Arquitetura

```
Canva App (Frontend)
    ↓
Canva.API (ASP.NET Core) - API intermediária
    ↓
TVPlayer API (Produção) - https://app.tvplayer.com.br:44909
```

## Passo 1: Configurar TVPlayer API

A API TVPlayer precisa implementar dois endpoints:

### GET /api/auth/validate
Valida token e retorna dados do usuário.

**Request:**
```
Authorization: Bearer {token}
```

**Response:**
```json
{
  "id": "123",
  "name": "Nome do Usuário",
  "email": "email@tvplayer.com.br"
}
```

### POST /api/media/upload
Recebe arquivo e metadados do usuário.

**Request:**
```
Authorization: Bearer {token}
Content-Type: multipart/form-data

file: [arquivo binário]
userId: "123"
userName: "Nome do Usuário"
```

**Response:**
```json
{
  "success": true,
  "fileId": "abc123",
  "originalName": "design.png",
  "size": 1024000,
  "url": "/uploads/abc123.png",
  "userId": "123",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

## Passo 2: Configurar Página de Login TVPlayer

A página de login precisa enviar mensagem para o Canva App após autenticação bem-sucedida:

```javascript
// Adicionar no final da página de login após autenticação
if (window.opener) {
  window.opener.postMessage({
    type: 'tvplayer_auth_success',
    token: 'TOKEN_AQUI',
    user: {
      id: '123',
      name: 'Nome do Usuário',
      email: 'email@tvplayer.com.br'
    }
  }, '*');
  window.close();
}
```

## Passo 3: Executar Canva.API

```bash
cd Canva.API/CanvaAPI
dotnet run
```

API rodará em: `https://localhost:7001`

## Passo 4: Executar Frontend Canva

```bash
npm start
```

Frontend rodará em: `http://localhost:8080`

## Passo 5: Testar no Canva

1. Acesse o Developer Portal do Canva
2. Clique em "Preview" no seu app
3. O app abrirá no Canva Editor
4. Clique em "Fazer Login na TVPlayer"
5. Faça login na página TVPlayer
6. Após login, você será redirecionado de volta ao app
7. Configure o endpoint: `https://localhost:7001/api/upload`
8. Exporte um design

## Fluxo de Autenticação

1. Usuário clica em "Fazer Login"
2. Popup abre com página de login TVPlayer
3. Usuário faz login
4. TVPlayer envia mensagem com token para o app
5. App salva token no localStorage
6. App valida token com Canva.API
7. Canva.API valida token com TVPlayer API
8. Usuário autenticado pode exportar

## Fluxo de Upload

1. Usuário seleciona formato e clica em "Exportar"
2. Canva exporta design no formato selecionado
3. App baixa o blob exportado
4. App envia arquivo + dados do usuário para Canva.API
5. Canva.API envia arquivo para TVPlayer API
6. TVPlayer API salva arquivo e retorna resposta
7. App mostra mensagem de sucesso

## Troubleshooting

### CORS Error
Verifique se a origem está configurada corretamente no `Program.cs`:
```csharp
policy.WithOrigins("http://localhost:8080", "https://app.canva.com")
```

### Token Inválido
Verifique se o endpoint de validação está correto em `appsettings.json`:
```json
"ValidateTokenEndpoint": "/api/auth/validate"
```

### Upload Falha
- Verifique tamanho do arquivo (máx 500MB)
- Verifique extensão permitida
- Verifique logs da API: `dotnet run --verbosity detailed`
