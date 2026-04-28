import { useState, useEffect } from "react";
import { Button, Rows, Text, TextInput, Title } from "@canva/app-ui-kit";
import { requestExport, ExportFileType } from "@canva/design";
import styles from "./styles.css";

const API_BASE_URL = "https://191.6.5.106:44909/canva.api";

type User = {
  id: string;
  name: string;
  email: string;
};

type AuthState = {
  isAuthenticated: boolean;
  user: User | null;
  token: string | null;
};

export default function App() {
  const [authState, setAuthState] = useState<AuthState>({
    isAuthenticated: false,
    user: null,
    token: null,
  });
  const [login, setLogin] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [exportFormat, setExportFormat] = useState<ExportFileType>("png");

  useEffect(() => {
    // Verificar se já existe token salvo
    const savedToken = localStorage.getItem("tvplayer_token");
    const savedUser = localStorage.getItem("tvplayer_user");
    
    if (savedToken && savedUser) {
      verifyToken(savedToken, JSON.parse(savedUser));
    }
  }, []);

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
        // Token inválido, limpar
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

      const data = await response.json();

      if (response.ok && data.success) {
        // Salvar token e dados do usuário
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
      setMessage("Erro ao conectar com o servidor");
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

    setLoading(true);
    setMessage("Exportando design...");

    try {
      // Exportar design do Canva
      const response = await requestExport({
        acceptedFileTypes: [exportFormat],
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
      
      // Baixar o blob
      const blobResponse = await fetch(exportedUrl);
      const blob = await blobResponse.blob();

      // Criar FormData para enviar
      const formData = new FormData();
      formData.append("file", blob, `design.${exportFormat}`);
      formData.append("userId", authState.user.id);
      formData.append("userName", authState.user.name);

      setMessage("Enviando para TVPlayer...");

      // Enviar para a API
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
      } else {
        setMessage(uploadData.error || "Erro ao enviar design");
      }
    } catch (error) {
      console.error("Erro no export:", error);
      setMessage("Erro ao exportar design");
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
            
            <TextInput
              placeholder="Senha"
              value={password}
              onChange={setPassword}
              disabled={loading}
            />
            
            <Button
              variant="primary"
              onClick={handleLogin}
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
            
            <Text size="small">
              Email: {authState.user?.email}
            </Text>

            <div className={styles.formatSelector}>
              <Text>Formato de exportação:</Text>
              <select 
                value={exportFormat} 
                onChange={(e) => setExportFormat(e.target.value as ExportFileType)}
                className={styles.select}
              >
                <option value="png">PNG</option>
                <option value="jpg">JPG</option>
                <option value="svg">SVG</option>
              </select>
            </div>

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
}
