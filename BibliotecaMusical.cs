using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CyberPlayer
{
    // Modelos de datos limpios y estructurados
    public class CancionInfo
    {
        public string Titulo { get; set; }
        public string RutaCompleta { get; set; }
        public string NombreArchivo { get; set; }
        public string Artista { get; set; }
        public string Album { get; set; }
    }

    public class AlbumInfo
    {
        public string Nombre { get; set; }
        public string Artista { get; set; }
        public List<CancionInfo> Canciones { get; set; } = new List<CancionInfo>();
    }

    public class ArtistaInfo
    {
        public string Nombre { get; set; }
        public List<AlbumInfo> Albumes { get; set; } = new List<AlbumInfo>();
    }

    // El cerebro del escaneo y organización musical
    public class BibliotecaMusical
    {
        public List<ArtistaInfo> Artistas { get; private set; } = new List<ArtistaInfo>();
        public List<AlbumInfo> TodosLosAlbumes { get; private set; } = new List<AlbumInfo>();
        public List<CancionInfo> TodasLasCanciones { get; private set; } = new List<CancionInfo>();

        // Escanea una carpeta completa y extrae los metadatos ID3 usando TagLib#
        public void CargarCarpeta(string rutaCarpeta)
        {
            // Limpiamos las listas anteriores por si carga otra carpeta distinta
            Artistas.Clear();
            TodosLosAlbumes.Clear();
            TodasLasCanciones.Clear();

            if (!Directory.Exists(rutaCarpeta)) return;

            // Buscamos todos los archivos mp3 en la carpeta y subcarpetas
            var archivos = Directory.GetFiles(rutaCarpeta, "*.mp3", SearchOption.AllDirectories);

            foreach (var archivo in archivos)
            {
                var infoArchivo = new FileInfo(archivo);
                string titulo = infoArchivo.Name;
                string artistaNombre = "Artista Desconocido";
                string albumNombre = "Álbum Desconocido";

                try
                {
                    // 🔥 Leemos los metadatos reales del archivo físico
                    using (var tagFile = TagLib.File.Create(archivo))
                    {
                        if (!string.IsNullOrEmpty(tagFile.Tag.Title))
                            titulo = tagFile.Tag.Title;

                        // Los artistas en TagLib vienen como un arreglo, agarramos el primero
                        if (tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0)
                            artistaNombre = tagFile.Tag.Performers[0];

                        if (!string.IsNullOrEmpty(tagFile.Tag.Album))
                            albumNombre = tagFile.Tag.Album;
                    }
                }
                catch
                {
                    // Si el archivo está corrupto o no tiene tags, mantiene los nombres por defecto
                }

                // Creamos el objeto de la canción
                var nuevaCancion = new CancionInfo
                {
                    Titulo = titulo,
                    RutaCompleta = archivo,
                    NombreArchivo = infoArchivo.Name,
                    Artista = artistaNombre,
                    Album = albumNombre
                };

                TodasLasCanciones.Add(nuevaCancion);

                // --- Clasificación en el Árbol de la Biblioteca ---

                // 1. Clasificar por Artista
                var artista = Artistas.FirstOrDefault(a => a.Nombre.Equals(artistaNombre, StringComparison.OrdinalIgnoreCase));
                if (artista == null)
                {
                    artista = new ArtistaInfo { Nombre = artistaNombre };
                    Artistas.Add(artista);
                }

                // 2. Clasificar por Álbum dentro de ese Artista
                var albumEnArtista = artista.Albumes.FirstOrDefault(al => al.Nombre.Equals(albumNombre, StringComparison.OrdinalIgnoreCase));
                if (albumEnArtista == null)
                {
                    albumEnArtista = new AlbumInfo { Nombre = albumNombre, Artista = artistaNombre };
                    artista.Albumes.Add(albumEnArtista);
                }
                albumEnArtista.Canciones.Add(nuevaCancion);

                // 3. Registrar en la lista global de todos los álbumes independientes
                var albumGlobal = TodosLosAlbumes.FirstOrDefault(al => al.Nombre.Equals(albumNombre, StringComparison.OrdinalIgnoreCase) && al.Artista.Equals(artistaNombre, StringComparison.OrdinalIgnoreCase));
                if (albumGlobal == null)
                {
                    albumGlobal = new AlbumInfo { Nombre = albumNombre, Artista = artistaNombre };
                    TodosLosAlbumes.Add(albumGlobal);
                }
                albumGlobal.Canciones.Add(nuevaCancion);
            }

            // Ordenamos alfabéticamente para que se vea limpio
            Artistas = Artistas.OrderBy(a => a.Nombre).ToList();
            TodosLosAlbumes = TodosLosAlbumes.OrderBy(al => al.Nombre).ToList();
        }
    }
}
