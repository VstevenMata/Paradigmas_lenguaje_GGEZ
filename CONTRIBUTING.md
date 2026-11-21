# Cómo contribuir a GGEZ

¡Gracias por tu interés! Esta guía explica cómo proponer cambios.

Al participar aceptas el [Código de Conducta](CODE_OF_CONDUCT.md).

## Antes de empezar

- **Bugs:** abre un issue con la plantilla *Reporte de bug*. Incluye el programa `.ggez` mínimo que reproduce el problema.
- **Ideas nuevas:** abre primero un issue con la plantilla *Propuesta*. Así se discute antes de que inviertas tiempo en un PR que quizá no encaje con el lenguaje (ver la [gramática](Traductor/GRAMATICA.md)).
- **Vulnerabilidades de seguridad:** no abras un issue público; sigue [SECURITY.md](SECURITY.md).

## Flujo de trabajo

La rama `main` está protegida: todo cambio entra por Pull Request y el CI tiene que pasar.

1. Crea una rama desde `main` (si no eres del equipo, primero haz un fork):
   ```sh
   git switch -c feat/descripcion-corta main
   ```
   Prefijos de rama: `feat/`, `fix/`, `docs/`, `refactor/`, `test/`, `ci/`, `chore/`.
2. Haz tus cambios con commits pequeños y claros (ver abajo).
3. Verifica localmente antes de abrir el PR:
   ```sh
   dotnet build ParadigmasLenguajePropio/ParadigmasLenguajePropio.sln
   cd Traductor && python ggezc.py compile ejemplos/hola.ggez
   ```
4. Abre el PR contra `main` y completa la plantilla.
5. El CI compila el IDE y los ejemplos del traductor. Con ✅ y las conversaciones resueltas, el PR se integra con *squash merge* y la rama se borra sola.

## Publicar una versión

El release es automático: sube `<Version>` en `ParadigmasLenguajePropio/src/ParadigmasLenguajePropio/ParadigmasLenguajePropio.csproj` dentro de tu PR. Al fusionarlo, GitHub Actions genera `GGEZ` y `GGEZ.exe`, crea el tag `vX.Y` y publica el release. Si la versión no cambió, no se publica nada. Los tags `v*` están protegidos: no se pueden mover ni borrar.

Después del release, actualiza en `flake.nix` la `url` y el `sha256` de `ggezRelease` (ver el README) en otro PR para que NixOS use la versión nueva.

## Mensajes de commit

Se usa [Conventional Commits](https://www.conventionalcommits.org/es/):

```
<tipo>(<alcance opcional>): <descripción en imperativo>
```

| Tipo | Uso |
|---|---|
| `feat` | Funcionalidad nueva |
| `fix` | Corrección de un bug |
| `docs` | Solo documentación |
| `refactor` | Cambio de código que no altera el comportamiento |
| `test` | Agregar o corregir pruebas |
| `ci` | GitHub Actions y automatización |
| `chore` | Mantenimiento (dependencias, configuración) |

Ejemplos: `feat(ide): pestaña de logros`, `fix(traductor): tamaño de la pila en recursión profunda`.

## Estilo de código

- IDE: C# con MVVM (Views / ViewModels / Services), sin advertencias nuevas al compilar.
- Traductor: Python 3.10 o superior, sin dependencias externas.
- Mensajes de error y documentación para el usuario: en español.

## Licencia de las contribuciones

Salvo que indiques lo contrario, toda contribución que envíes para su inclusión en el proyecto se licencia bajo [MIT](LICENSE-MIT) o [Apache-2.0](LICENSE-APACHE), a elección del usuario, sin condiciones adicionales.
