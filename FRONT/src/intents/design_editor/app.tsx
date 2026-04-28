import { useState, useEffect } from "react";
import { Button, Rows, Text, TextInput, Title, FormField} from "@canva/app-ui-kit";
import * as styles from "styles/components.css";
import { requestExport, getDesignMetadata } from "@canva/design";

const API_BASE_URL = "https://localhost:7287/api";

type User = {
  id: string;
  name: string;
  email?: string;
};

type AuthState = {
  isAuthenticated: boolean;
  user: User | null;
  token: string | null;
};

type ExportFormat = "png" | "jpg" | "svg" | "gif" | "mp4";

export const App = () => {
  const [authState, setAuthState] = useState<AuthState>({
    isAuthenticated: false,
    user: null,
    token: null,
  });
  const [login, setLogin] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [exportFormat, setExportFormat] = useState<ExportFormat>("png");
  const [templateName, setTemplateName] = useState("");

  useEffect(() => {
    console.log("App montado! API URL:", API_BASE_URL);
    const savedToken = localStorage.getItem("tvplayer_token");
    const savedUser = localStorage.getItem("tvplayer_user");
    
    if (savedToken && savedUser) {
      console.log("Token encontrado no localStorage");
      verifyToken(savedToken, JSON.parse(savedUser));
    }
  }, []);

// Para imagens (jpg, png)
function getImageDimensions(blob: Blob): Promise<{ width: number; height: number }> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(blob);
    const img = new Image();
    img.onload = () => {
      resolve({ width: img.naturalWidth, height: img.naturalHeight });
      URL.revokeObjectURL(url);
    };
    img.onerror = () => {
      resolve({ width: 0, height: 0 });
      URL.revokeObjectURL(url);
    };
    img.src = url;
  });
}

// ✅ Para vídeos (mp4)
function getVideoDimensions(blob: Blob): Promise<{ width: number; height: number }> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(blob);
    const video = document.createElement("video");
    video.onloadedmetadata = () => {
      resolve({ width: video.videoWidth, height: video.videoHeight });
      URL.revokeObjectURL(url);
    };
    video.onerror = () => {
      resolve({ width: 0, height: 0 });
      URL.revokeObjectURL(url);
    };
    video.src = url;
  });
}
  const verifyToken = async (token: string, user: User) => {
    try {
      const response = await fetch(`${API_BASE_URL}/auth/verify`, {
        method: "POST",
        headers: {
          "Authorization": `Bearer ${token}`,
          "Content-Type": "application/json",
        },
      });

      if (response.ok) {
        setAuthState({
          isAuthenticated: true,
          user,
          token,
        });
      } else {
        localStorage.removeItem("tvplayer_token");
        localStorage.removeItem("tvplayer_user");
      }
    } catch (error) {
      console.error("Erro ao verificar token:", error);
    }
  };

  const handleLogin = async () => {
    if (!login || !password) {
      setMessage("Preencha usuário e senha");
      return;
    }

    setLoading(true);
    setMessage("");

    try {
      console.log("Tentando login em:", `${API_BASE_URL}/auth/login`);
      
      const response = await fetch(`${API_BASE_URL}/auth/login`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          login,
          password,
        }),
      });

      console.log("Status da resposta:", response.status);
      
      const data = await response.json();
      console.log("Dados recebidos:", data);

      if (response.ok && data.success) {
        localStorage.setItem("tvplayer_token", data.token);
        localStorage.setItem("tvplayer_user", JSON.stringify(data.user));

        setAuthState({
          isAuthenticated: true,
          user: data.user,
          token: data.token,
        });

        setMessage("Login realizado com sucesso!");
        setLogin("");
        setPassword("");
      } else {
        setMessage(data.error || "Erro ao fazer login");
      }
    } catch (error) {
      console.error("Erro no login:", error);
      setMessage(`Erro ao conectar: ${error instanceof Error ? error.message : 'Verifique se a API está rodando em https://localhost:7287'}`);
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem("tvplayer_token");
    localStorage.removeItem("tvplayer_user");
    setAuthState({
      isAuthenticated: false,
      user: null,
      token: null,
    });
    setMessage("Logout realizado");
  };

  const handleExport = async () => {
    if (!authState.token || !authState.user) {
      setMessage("Você precisa estar autenticado");
      return;
    }

    if (!templateName.trim()) {
      setMessage("Por favor, dê um nome ao template");
      return;
    }

    setLoading(true);
    setMessage("Exportando design...");

    try {
     const metadata = await getDesignMetadata();
    console.log("Metadata do design:", metadata);
      
      if (!metadata) {
        setMessage("Não foi possível obter dimensões do design");
        setLoading(false);
        return;
      }
      
      // MP4 usa o tipo "video" no Canva SDK
     const fileType = exportFormat === "mp4" ? "video" : exportFormat;

     const response = await requestExport({
  acceptedFileTypes: [fileType as "video" | "jpg" | "png" ], // ✅ cast direto
});

      if (response.status !== "completed") {
        setMessage("Exportação cancelada");
        setLoading(false);
        return;
      }

      if (!response.exportBlobs || response.exportBlobs.length === 0) {
        setMessage("Nenhum arquivo exportado");
        setLoading(false);
        return;
      }

      const exportedUrl = response.exportBlobs[0]?.url;
      
      if (!exportedUrl) {
        setMessage("URL de exportação não encontrada");
        setLoading(false);
        return;
      }
      
      const blobResponse = await fetch(exportedUrl);
      const blob = await blobResponse.blob();

      const { width, height } = exportFormat === "mp4"
  ? await getVideoDimensions(blob)
  : await getImageDimensions(blob);

      const formData = new FormData();
      const fileName = `${templateName}.${exportFormat}`;
      formData.append("file", blob, fileName);
      formData.append("userId", authState.user.id);
      formData.append("userName", authState.user.name);
      formData.append("width", String(width));
      formData.append("height", String(height));

      setMessage("Enviando para TVPlayer...");

      const uploadResponse = await fetch(`${API_BASE_URL}/upload`, {
        method: "POST",
        headers: {
          "Authorization": `Bearer ${authState.token}`,
        },
        body: formData,
      });

      const uploadData = await uploadResponse.json();

      if (uploadResponse.ok && uploadData.success) {
        setMessage("Design enviado com sucesso!");
        setTemplateName(""); // Limpar o nome após sucesso
      } else {
        setMessage(uploadData.error || "Erro ao enviar design");
      }
    } catch (error) {
      console.error("Erro no export:", error);
      setMessage(`Erro ao exportar: ${error instanceof Error ? error.message : "Erro desconhecido"}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.scrollContainer}>
      <Rows spacing="2u">
        <Title>TVPlayer - Canva Integration</Title>

        {!authState.isAuthenticated ? (
          <>
            <Text>Faça login para exportar designs para o TVPlayer</Text>
            
            <TextInput
              placeholder="Usuário"
              value={login}
              onChange={setLogin}
              disabled={loading}
            />
            


        <FormField
          label="Senha"
          control={() => (
            <input
              type="password"
              placeholder = "Senha"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              style={{
                width: "100%",
                padding: "8px 12px",
                borderRadius: "6px",
                border: "1px solid #e0e0e0",
                fontSize: "14px",
                outline: "none",
                boxSizing: "border-box",
              }}
            />
          )}
        />

            
            <Button
              variant="primary"
              onClick={() => {
                console.log("Botão de login clicado!");
                console.log("Login:", login);
                console.log("Password:", password ? "***" : "vazio");
                handleLogin();
              }}
              loading={loading}
              stretch
            >
              Fazer Login
            </Button>
          </>
        ) : (
          <>
            <Text>
              Bem-vindo, {authState.user?.name}!
            </Text>

            <Rows spacing="1u">
              <Text>Nome do template:</Text>
              <TextInput
                placeholder="Ex: Banner Promocional"
                value={templateName}
                onChange={setTemplateName}
                disabled={loading}
              />
            </Rows>

            <Rows spacing="1u">
              <Text>Formato de exportação:</Text>
              <select 
                value={exportFormat} 
                onChange={(e) => setExportFormat(e.target.value as ExportFormat)}
                style={{
                  padding: "8px 12px",
                  border: "1px solid #d1d5db",
                  borderRadius: "6px",
                  fontSize: "14px",
                  backgroundColor: "white",
                  cursor: "pointer",
                }}
              >
                <optgroup label="Imagens">
                  <option value="png">PNG</option>
                  <option value="jpg">JPG</option>
                  <option value="svg">SVG</option>
                  <option value="gif">GIF</option>
                </optgroup>
                <optgroup label="Vídeos">
                  <option value="mp4">MP4</option>
                </optgroup>
              </select>
            </Rows>

            <Button
              variant="primary"
              onClick={handleExport}
              loading={loading}
              stretch
            >
              Exportar para TVPlayer
            </Button>

            <Button
              variant="secondary"
              onClick={handleLogout}
              disabled={loading}
              stretch
            >
              Sair
            </Button>
          </>
        )}

        {message && (
          <Text size="small">
            {message}
          </Text>
        )}
      </Rows>
    </div>
  );
};
