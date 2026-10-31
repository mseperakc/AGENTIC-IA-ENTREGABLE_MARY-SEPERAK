/* =====================================================================
   SEE - Comportamiento del front (Fase F de la migración)
   Solo actúa en el navegador: no cambia los ID ni los eventos de servidor.
   Requiere jQuery 1.7 y Kendo UI (kendoDatePicker), cargados en Site.Master.
   ===================================================================== */
var SEE = (function ($) {
    'use strict';

    var HOY = new Date();
    HOY.setHours(0, 0, 0, 0);

    var CLAVE_VISTA = 'see.vista';
    var CLAVE_DENSIDAD = 'see.densidad';
    var CLAVE_CONSULTA = 'see.consulta.';

    // Grillas con casilla de selección: texto del contador y del botón Procesar
    var SELECCION = {
        comprobantes: { uno: 'seleccionado', varios: 'seleccionados' },
        retenciones: { uno: 'seleccionada', varios: 'seleccionadas' }
    };

    // ---------- Almacenamiento local (puede no estar disponible) ----------

    function leer(almacen, clave) {
        try { return window[almacen].getItem(clave); } catch (e) { return null; }
    }

    function guardar(almacen, clave, valor) {
        try { window[almacen].setItem(clave, valor); } catch (e) { /* sin almacenamiento */ }
    }

    // ---------- Utilidades ----------

    function texto(celda) {
        return $.trim($(celda).text().replace(/ /g, ' '));
    }

    function plural(n, uno, varios) {
        return n + ' ' + (n === 1 ? uno : varios);
    }

    function filasDeDatos($grilla) {
        // Filas con celdas <td>, sin el paginador ni los totales
        return $grilla.find('tr').filter(function () {
            var $f = $(this);
            return $f.children('td').length > 1 && !$f.hasClass('see-paginador') && !$f.hasClass('see-total');
        });
    }

    function numero(textoCelda) {
        // Formato es-PE: 1,234.56
        var v = parseFloat(String(textoCelda).replace(/[^0-9.\-]/g, ''));
        return isNaN(v) ? 0 : v;
    }

    function formatoSoles(v) {
        return v.toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function totalConsultado($mensaje) {
        // "Se encontraron N ..." escrito por el servidor
        var m = /(\d+)/.exec($mensaje.text() || '');
        return m ? parseInt(m[1], 10) : null;
    }

    // ---------- Navegación ----------

    function marcarSeccionActiva() {
        var pagina = (location.pathname.split('/').pop() || '').toLowerCase();
        $('.see-nav a').each(function () {
            if (($(this).attr('href') || '').toLowerCase() === pagina) {
                $(this).addClass('activo').attr('aria-current', 'page');
            }
        });
    }

    // ---------- Pestañas ----------

    function mostrarVista(vista) {
        var $pestanas = $('.see-pestanas button');
        if (!$pestanas.filter('[data-vista="' + vista + '"]').length) vista = $pestanas.first().attr('data-vista');
        $pestanas.each(function () {
            $(this).attr('aria-selected', $(this).attr('data-vista') === vista ? 'true' : 'false');
        });
        $('.see-panel').each(function () {
            if ($(this).attr('data-vista') === vista) $(this).removeAttr('hidden');
            else $(this).attr('hidden', 'hidden');
        });
        guardar('sessionStorage', CLAVE_VISTA, vista);
    }

    function iniciarPestanas() {
        var $contenedor = $('[data-vista-inicial]');
        if (!$contenedor.length) return;

        // En un postback se vuelve a la pestaña desde la que se envió; en una carga nueva, a la inicial
        var esPostback = $contenedor.attr('data-postback') === '1';
        var vista = (esPostback && leer('sessionStorage', CLAVE_VISTA)) || $contenedor.attr('data-vista-inicial');
        mostrarVista(vista);

        $('.see-pestanas').on('click', 'button', function () {
            mostrarVista($(this).attr('data-vista'));
        });

        // Recordar la pestaña de cualquier control que haga postback
        $(document).on('click change', '.see-panel :input', function () {
            guardar('sessionStorage', CLAVE_VISTA, $(this).closest('.see-panel').attr('data-vista'));
        });
    }

    // ---------- Fechas ----------

    function validarTexto(valor, noFutura) {
        if (!valor) return { fecha: null, error: 'Ingrese la fecha' };
        var m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(valor);
        if (!m) return { fecha: null, error: 'Use el formato dd/mm/aaaa (ej. 05/10/2026)' };
        var d = new Date(+m[3], +m[2] - 1, +m[1]);
        if (d.getDate() !== +m[1] || d.getMonth() !== +m[2] - 1) return { fecha: null, error: 'Esa fecha no existe' };
        if (noFutura && d > HOY) return { fecha: d, error: 'No puede ser una fecha futura' };
        return { fecha: d, error: '' };
    }

    function mostrarError($input, mensaje) {
        var $campo = $input.closest('.see-campo');
        $campo.find('.see-error').remove();
        $campo.toggleClass('con-error', !!mensaje);
        $input.attr('aria-invalid', mensaje ? 'true' : 'false');
        if (mensaje) $('<span class="see-error" role="alert"></span>').text(mensaje).appendTo($campo);
    }

    function iniciarFechas() {
        if (window.kendo && kendo.culture) {
            try { kendo.culture('es-PE'); } catch (e) { /* cultura no cargada */ }
        }
        $('input.see-fecha').each(function () {
            var $input = $(this);
            var opciones = { format: 'dd/MM/yyyy', parseFormats: ['dd/MM/yyyy'] };
            if ($input.attr('data-nofutura') === '1') opciones.max = HOY;
            if ($.fn.kendoDatePicker) $input.kendoDatePicker(opciones);
            $input.attr({ placeholder: 'dd/mm/aaaa', inputmode: 'numeric', autocomplete: 'off' });
            // Limpia el error en cuanto se corrige
            $input.on('change keyup', function () {
                if ($input.closest('.see-campo').hasClass('con-error')) validarCampo($input);
            });
        });
    }

    function validarCampo($input) {
        var r = validarTexto($.trim($input.val()), $input.attr('data-nofutura') === '1');
        mostrarError($input, r.error);
        return r;
    }

    // Valida las fechas del grupo (data-grupo). Se usa en OnClientClick de los botones.
    function validar(grupo) {
        var ok = true, desde = null, $hasta = null;
        $('input.see-fecha[data-grupo="' + grupo + '"]').each(function () {
            var $input = $(this);
            var r = validarCampo($input);
            if (r.error) { ok = false; return; }
            if ($input.attr('data-rango') === 'desde') desde = r.fecha;
            if ($input.attr('data-rango') === 'hasta') $hasta = $input;
        });
        if (ok && desde && $hasta) {
            var hasta = validarTexto($.trim($hasta.val()), false).fecha;
            if (hasta < desde) {
                mostrarError($hasta, 'Debe ser igual o posterior a «Desde»');
                ok = false;
            }
        }
        return ok;
    }

    // Valida y recuerda la fecha consultada, para avisar si cambia antes de procesar
    function consultar(grupo) {
        if (!validar(grupo)) return false;
        guardar('sessionStorage', CLAVE_CONSULTA + grupo, $.trim($('input.see-fecha[data-grupo="' + grupo + '"]').first().val()));
        return true;
    }

    // ---------- Diálogo de confirmación ----------

    var botonPendiente = null;

    function confirmar(boton, datos) {
        // Segundo clic, ya confirmado: deja pasar el postback
        if (boton.getAttribute('data-confirmado') === '1') {
            boton.removeAttribute('data-confirmado');
            return true;
        }
        var $modal = $('#see-modal');
        $('#see-modal-titulo').text(datos.titulo);
        var $lineas = $('#see-modal-lineas').empty();
        $.each(datos.lineas || [], function (i, l) { $('<li></li>').text(l).appendTo($lineas); });
        $('#see-modal-aviso').text(datos.aviso || '');
        var $aceptar = $('#see-modal-aceptar');
        if (datos.boton) $aceptar.text(datos.boton).show(); else $aceptar.hide();
        $('#see-modal-cancelar').text(datos.boton ? 'Cancelar' : 'Entendido');
        botonPendiente = boton;
        $modal.removeAttr('hidden');
        (datos.boton ? $aceptar : $('#see-modal-cancelar')).focus();
        return false;
    }

    function cerrarModal() {
        $('#see-modal').attr('hidden', 'hidden');
        if (botonPendiente) botonPendiente.focus();
        botonPendiente = null;
    }

    function iniciarModal() {
        $('#see-modal-cancelar').on('click', cerrarModal);
        $('#see-modal-aceptar').on('click', function () {
            var boton = botonPendiente;
            $('#see-modal').attr('hidden', 'hidden');
            botonPendiente = null;
            if (boton) {
                boton.setAttribute('data-confirmado', '1');
                boton.click();
            }
        });
        $(document).on('keydown', function (e) {
            if (e.which === 27 && !$('#see-modal').is('[hidden]')) cerrarModal();
        });
    }

    // ---------- Grillas con selección (comprobantes y retenciones) ----------

    function marcarEstados($grilla) {
        // Estado con color y texto
        $grilla.find('td.col-estado').each(function () {
            var t = texto(this);
            if (!t) return;
            var clase = /ACEPT/i.test(t) ? 'aceptado' : /RECHAZ/i.test(t) ? 'rechazado' : /ERROR|EXCEP|FALL/i.test(t) ? 'error' : '';
            $(this).empty().append($('<span class="see-estado"></span>').addClass(clase).text(t));
        });
        // Retenciones y reversiones no traen el estado: se deduce del código CDR (0 = aceptado)
        $grilla.find('td.col-cdr').each(function () {
            var t = texto(this);
            if (!t) { $(this).empty().append('<span class="tenue">Sin enviar</span>'); return; }
            var aceptado = t === '0';
            $(this).empty().attr('title', 'Código CDR ' + t).append(
                $('<span class="see-estado"></span>').addClass(aceptado ? 'aceptado' : 'rechazado').text(aceptado ? 'Aceptado' : 'Rechazado · ' + t));
        });
        // Texto completo al pasar el mouse en las columnas recortadas
        $grilla.find('td.recorte').each(function () { $(this).attr('title', texto(this)); });
    }

    function iniciarGrillaSeleccion(nombre) {
        var $grilla = $('.see-grilla[data-grilla="' + nombre + '"]');
        if (!$grilla.length) return;
        marcarEstados($grilla);
        $grilla.on('change', 'td input[type="checkbox"]', function () { actualizarSeleccion(nombre); });
        actualizarSeleccion(nombre);
    }

    function seleccion(nombre) {
        var $grilla = $('.see-grilla[data-grilla="' + nombre + '"]');
        var todasLasPaginas = $grilla.find('th input[type="checkbox"]').is(':checked');
        var marcadas = filasDeDatos($grilla).filter(function () {
            return $(this).find('td:first input[type="checkbox"]').is(':checked');
        });
        return { $grilla: $grilla, marcadas: marcadas, todas: todasLasPaginas };
    }

    function actualizarSeleccion(nombre) {
        var s = seleccion(nombre);
        var palabra = SELECCION[nombre];
        filasDeDatos(s.$grilla).each(function () {
            $(this).toggleClass('seleccionada', $(this).find('td:first input[type="checkbox"]').is(':checked'));
        });
        var total = totalConsultado($('[data-mensaje="' + nombre + '"]'));
        var n = s.todas && total ? total : s.marcadas.length;
        // El contador y el botón Procesar están en la misma pestaña que la grilla
        var $ambito = s.$grilla.closest('.see-panel');
        if (!$ambito.length) $ambito = $(document.body);
        $ambito.find('[data-ficha="seleccionados"]').text(plural(n, palabra.uno, palabra.varios));
        var $procesar = $ambito.find('[data-accion="procesar-' + nombre + '"]');
        $procesar.val(n ? 'Procesar ' + plural(n, palabra.uno, palabra.varios) : 'Procesar ' + palabra.varios);
        $procesar.toggleClass('inactivo', n === 0);
    }

    function confirmarComprobantes(boton) {
        var s = seleccion('comprobantes');
        if (!s.marcadas.length && !s.todas) {
            return confirmar(boton, { titulo: 'No hay comprobantes seleccionados', lineas: ['Marque en la lista los comprobantes que quiere procesar.'] });
        }
        var total = totalConsultado($('[data-mensaje="comprobantes"]'));
        var lineas = [];
        if (s.todas) {
            lineas.push('Todos los comprobantes de la consulta' + (total ? ' (' + total + ')' : '') + ', en todas las páginas.');
        } else {
            s.marcadas.slice(0, 8).each(function () {
                var c = $(this).children('td');
                lineas.push(texto(c[3]) + '-' + texto(c[4]) + ' · ' + texto(c[6]) + ' · S/ ' + texto(c[7]));
            });
            if (s.marcadas.length > 8) lineas.push('… y ' + plural(s.marcadas.length - 8, 'comprobante más', 'comprobantes más'));
        }
        var n = s.todas && total ? total : s.marcadas.length;
        return confirmar(boton, {
            titulo: '¿Procesar ' + plural(n, 'comprobante', 'comprobantes') + '?',
            lineas: lineas,
            aviso: 'Se generará el XML firmado y se enviará a SUNAT/OSE. Un comprobante aceptado no se puede retirar.',
            boton: 'Sí, procesar'
        });
    }

    // ---------- Retenciones y reversiones ----------

    function confirmarRetenciones(boton) {
        var s = seleccion('retenciones');
        if (!s.marcadas.length && !s.todas) {
            return confirmar(boton, { titulo: 'No hay retenciones seleccionadas', lineas: ['Pulse Consultar y marque en la lista las retenciones que quiere procesar.'] });
        }
        var total = totalConsultado($('[data-mensaje="retenciones"]'));
        var lineas = [];
        if (s.todas) {
            lineas.push('Todas las retenciones de la consulta' + (total ? ' (' + total + ')' : '') + ', en todas las páginas.');
        } else {
            s.marcadas.slice(0, 8).each(function () {
                var c = $(this).children('td');
                lineas.push(texto(c[2]) + '-' + texto(c[3]) + ' · ' + texto(c[6]) + ' · retenido S/ ' + texto(c[7]));
            });
            if (s.marcadas.length > 8) lineas.push('… y ' + plural(s.marcadas.length - 8, 'retención más', 'retenciones más'));
        }
        var n = s.todas && total ? total : s.marcadas.length;
        return confirmar(boton, {
            titulo: '¿Procesar ' + plural(n, 'retención', 'retenciones') + '?',
            lineas: lineas,
            aviso: 'Se generará el XML firmado y se enviará a SUNAT/OSE. Una retención aceptada solo se anula con una reversión.',
            boton: 'Sí, procesar'
        });
    }

    function confirmarReversion(boton) {
        if (!validar('rev')) return false;
        var $filas = filasDeDatos($('.see-grilla[data-grilla="reversiones"]'));
        if (!$filas.length) {
            return confirmar(boton, { titulo: 'No hay reversiones para procesar', lineas: ['Pulse Consultar con la fecha de comunicación y revise la lista antes de procesar.'] });
        }
        var fecha = $.trim($('input.see-fecha[data-grupo="rev"]').val());
        var consultada = leer('sessionStorage', CLAVE_CONSULTA + 'rev');
        if (consultada && consultada !== fecha) {
            return confirmar(boton, { titulo: 'La fecha cambió', lineas: ['La lista es del ' + consultada + ' y la fecha escrita es ' + fecha + '.', 'Pulse Consultar antes de procesar.'] });
        }
        var lineas = [];
        $filas.slice(0, 8).each(function () {
            var c = $(this).children('td');
            lineas.push(texto(c[1]) + '-' + texto(c[2]) + ' · ' + (texto(c[6]) || 'sin motivo'));
        });
        if ($filas.length > 8) lineas.push('… y ' + plural($filas.length - 8, 'reversión más', 'reversiones más'));
        return confirmar(boton, {
            titulo: '¿Enviar la reversión del ' + (consultada || fecha) + '?',
            lineas: lineas,
            aviso: 'Se enviarán todas las reversiones de la lista en una sola comunicación. Una reversión aceptada anula la retención y no se puede deshacer.',
            boton: 'Sí, revertir'
        });
    }

    // ---------- Resumen diario y comunicación de baja ----------

    function fechaGrillaAFormato(t) {
        // La grilla muestra dd/MM/yyyy
        return /^\d{2}\/\d{2}\/\d{4}$/.test(t) ? t : '';
    }

    function marcarSinTicket($grilla) {
        $grilla.find('td.col-ticket').each(function () {
            if (!texto(this)) $(this).empty().append('<span class="tenue">Sin enviar</span>');
        });
    }

    function iniciarGrillaResumen() {
        var $grilla = $('.see-grilla[data-grilla="resumen"]');
        if (!$grilla.length) return;
        marcarSinTicket($grilla);

        // Totales de las columnas numéricas (de la página visible)
        var $filas = filasDeDatos($grilla);
        if (!$filas.length) return;
        var $primera = $filas.first().children('td');
        var totales = [];
        $primera.each(function (i) { totales[i] = $(this).hasClass('sumar') ? 0 : null; });
        $filas.each(function () {
            $(this).children('td').each(function (i) {
                if (totales[i] !== null) totales[i] += numero(texto(this));
            });
        });
        var paginado = $grilla.find('tr.see-paginador').length > 0;
        var $total = $('<tr class="see-total"></tr>');
        $.each(totales, function (i, v) {
            var $td = $('<td></td>');
            if (i === 0) $td.text(paginado ? 'Total de esta página' : 'Total del día');
            if (v !== null) $td.addClass('num').text(formatoSoles(v));
            $total.append($td);
        });
        var $paginador = $grilla.find('tr.see-paginador');
        if ($paginador.length) $paginador.before($total); else $filas.last().after($total);
    }

    function iniciarGrillaBajas() {
        var $grilla = $('.see-grilla[data-grilla="bajas"]');
        if (!$grilla.length) return;
        marcarSinTicket($grilla);
        $grilla.find('td.col-motivo').each(function () {
            if (!texto(this)) $(this).empty().append('<span class="tenue">Sin motivo · se enviará «CANCELACIÓN POR HOMOLOGACIÓN»</span>');
        });
    }

    function fechaConsultada($grilla) {
        var $f = filasDeDatos($grilla).first();
        return $f.length ? fechaGrillaAFormato(texto($f.children('td')[0])) : '';
    }

    function confirmarResumen(boton) {
        if (!validar('rd')) return false;
        var $grilla = $('.see-grilla[data-grilla="resumen"]');
        var $filas = filasDeDatos($grilla);
        if (!$grilla.length || !$filas.length) return true; // el servidor responde "No existe información a procesar"
        var fecha = $.trim($('input.see-fecha[data-grupo="rd"]').val());
        var consultada = fechaConsultada($grilla);
        if (consultada && consultada !== fecha) {
            return confirmar(boton, { titulo: 'La fecha cambió', lineas: ['La lista es del ' + consultada + ' y la fecha escrita es ' + fecha + '.', 'Pulse Consultar antes de procesar.'] });
        }
        var n = totalConsultado($('[data-mensaje="resumen"]')) || $filas.length;
        var bloques = Math.floor(n / 500) + 1;
        var conTicket = $grilla.find('td.col-ticket a').filter(function () { return !!texto(this); }).length;
        return confirmar(boton, {
            titulo: (conTicket ? '¿Reenviar' : '¿Enviar') + ' el resumen diario del ' + fecha + '?',
            lineas: [plural(n, 'comprobante', 'comprobantes') + ' en ' + plural(bloques, 'resumen', 'resúmenes') + ' (máx. 500 por resumen).'],
            aviso: conTicket ? 'Este día ya tiene ticket. Reenviar genera un resumen nuevo y reemplaza el ticket guardado.' : 'Se enviarán todos los comprobantes de la lista. SUNAT devolverá un ticket por resumen.',
            boton: conTicket ? 'Sí, reenviar' : 'Sí, enviar resumen'
        });
    }

    function confirmarBaja(boton) {
        if (!validar('cb')) return false;
        var $grilla = $('.see-grilla[data-grilla="bajas"]');
        var $filas = filasDeDatos($grilla);
        if (!$grilla.length || !$filas.length) return true;
        var fecha = $.trim($('input.see-fecha[data-grupo="cb"]').val());
        var consultada = fechaConsultada($grilla);
        if (consultada && consultada !== fecha) {
            return confirmar(boton, { titulo: 'La fecha cambió', lineas: ['La lista es del ' + consultada + ' y la fecha escrita es ' + fecha + '.', 'Pulse Consultar antes de procesar.'] });
        }
        var lineas = [];
        $filas.slice(0, 8).each(function () {
            var c = $(this).children('td');
            lineas.push(texto(c[2]) + '-' + texto(c[3]) + ' · ' + (texto(c[4]) || 'CANCELACIÓN POR HOMOLOGACIÓN'));
        });
        if ($filas.length > 8) lineas.push('… y ' + plural($filas.length - 8, 'baja más', 'bajas más'));
        return confirmar(boton, {
            titulo: '¿Enviar la comunicación de baja del ' + fecha + '?',
            lineas: lineas,
            aviso: 'Se enviarán todas las bajas de la lista. Las bajas aceptadas por SUNAT anulan los comprobantes y no se pueden revertir.',
            boton: 'Sí, dar de baja'
        });
    }

    // ---------- Densidad de filas ----------

    function aplicarDensidad(valor) {
        $('.see-grilla').toggleClass('normal', valor === 'normal');
        $('.see-densidad button').each(function () {
            $(this).attr('aria-pressed', $(this).attr('data-densidad') === valor ? 'true' : 'false');
        });
        guardar('localStorage', CLAVE_DENSIDAD, valor);
    }

    function iniciarDensidad() {
        aplicarDensidad(leer('localStorage', CLAVE_DENSIDAD) || 'compacta');
        $(document).on('click', '.see-densidad button', function () {
            aplicarDensidad($(this).attr('data-densidad'));
        });
    }

    // Recuadro de grilla sin tabla (el servidor la oculta cuando no hay filas).
    // data-vacio permite otro texto, por ejemplo antes de la primera consulta.
    function marcarGrillasVacias() {
        $('.see-desplaza').each(function () {
            if (!$(this).find('table').length) {
                var aviso = $(this).attr('data-vacio') || 'No hay registros para mostrar con estos datos.';
                $(this).replaceWith($('<div class="see-pie"></div>').text(aviso));
            }
        });
    }

    $(function () {
        marcarGrillasVacias();
        marcarSeccionActiva();
        iniciarPestanas();
        iniciarFechas();
        iniciarModal();
        iniciarGrillaSeleccion('comprobantes');
        iniciarGrillaSeleccion('retenciones');
        iniciarGrillaResumen();
        iniciarGrillaBajas();
        marcarEstados($('.see-grilla[data-grilla="reversiones"]'));
        iniciarDensidad();
    });

    return {
        validar: validar,
        consultar: consultar,
        confirmarComprobantes: confirmarComprobantes,
        confirmarResumen: confirmarResumen,
        confirmarBaja: confirmarBaja,
        confirmarRetenciones: confirmarRetenciones,
        confirmarReversion: confirmarReversion
    };
})(jQuery);
