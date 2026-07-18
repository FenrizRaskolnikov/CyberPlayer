# CyberPlayer

¡Bienvenido a **CyberPlayer**! Un reproductor de música de escritorio nativo para Windows desarrollado en **C# y WPF**, diseñado con una estética Cyberpunk neón (fucsia y verde neón) y optimizado para ofrecer una reproducción fluida y sin fricciones.

---

## Características Principales

*   **Interfaz Cybergoth Estilizada:** Un diseño oscuro, y de alto contraste utilizando estilos avanzados en XAML.
*   **Reproducción Ininterrumpida (Seamless Audio):** Gracias a una arquitectura basada en la duplicación temporal de archivos de audio, puedes escuchar tus pistas favoritas de fondo de manera continua.
*   **Control de Audio de Alta Precisión:** Motor multimedia potenciado por **NAudio** con monitor de volumen en tiempo real (Volume Meter), slider de progreso interactivo y control de volumen integrado.
*   **Gestión de Cola de Reproducción:** Cola inteligente integrada que sincroniza el índice visual de la lista con las canciones reales en cola.

---

## Tecnologías Utilizadas

*   **Lenguaje:** C# (.NET)
*   **Interfaz Gráfica:** WPF (Windows Presentation Foundation) con XAML.
*   **Motor de Audio:** [NAudio](https://github.com/naudio/NAudio) para la decodificación, canales de muestreo y eventos de reproducción de audio.
*   **Manipulación de Archivos:** [TagLib#](https://github.com/mono/taglib-sharp) para la lectura y escritura de metadatos ID3 en archivos multimedia.



---

## Requisitos e Instalación

1. Asegúrate de tener instalado el SDK de **.NET 6.0 / 7.0 / 8.0** (o superior) y **Visual Studio**.
2. Clona este repositorio en tu máquina local:
   ```bash
   git clone [https://github.com/FenrizRaskolnikov/CyberPlayer.git](https://github.com/FenrizRaskolnikov/CyberPlayer.git)
3. Abre el archivo de solución .sln en Visual Studio.
4. Restaura los paquetes NuGet necesarios (NAudio y TagLibSharp).
5. Compila y ejecuta el proyecto
