// Punto de entrada del portal docente (se carga como módulo desde _Layout.cshtml).
// Cada funcionalidad vive en su propio archivo dentro de js/portal/. Sin dependencias externas.
import { iniciarModales } from './portal/modales.js';
import { iniciarConfirmaciones } from './portal/confirmar.js';
import { iniciarAvisos } from './portal/avisos.js';
import { iniciarGrids } from './portal/grid.js';
import { iniciarFormularios } from './portal/formularios.js';

iniciarModales();
iniciarConfirmaciones();
iniciarAvisos();
iniciarGrids();
iniciarFormularios();
