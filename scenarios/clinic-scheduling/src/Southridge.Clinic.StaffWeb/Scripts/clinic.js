/// <reference name="MicrosoftAjax.js" />
// Shared client script for the staff site (loaded by the ScriptManager on Site.Master).
Type.registerNamespace('clinic');

// AutoCompleteExtender OnClientItemSelected: copy the patient id into the hidden field next to the text box.
clinic.patientSelected = function (source, e) {
    var box = source.get_element();
    var hidden = clinic.findHidden(box);
    if (hidden) {
        hidden.value = e.get_value();
    }
};

clinic.findHidden = function (box) {
    var node = box.nextSibling;
    while (node) {
        if (node.nodeType === 1 && node.tagName === 'INPUT' && node.type === 'hidden') {
            return node;
        }
        node = node.nextSibling;
    }
    return null;
};

// Typing in a patient box after a pick clears the old id so a stale patient is never booked.
clinic.watchPatientBoxes = function () {
    var inputs = document.getElementsByTagName('input');
    for (var i = 0; i < inputs.length; i++) {
        var box = inputs[i];
        if (box.type === 'text' && box.getAttribute('autocomplete') === 'off' && !box.clinicWatched) {
            box.clinicWatched = true;
            $addHandler(box, 'keydown', clinic.patientTyped);
        }
    }
};

clinic.patientTyped = function (e) {
    var key = e.keyCode;
    if (key === 9 || key === 13 || (key >= 37 && key <= 40)) {
        return;
    }
    var hidden = clinic.findHidden(e.target);
    if (hidden) {
        hidden.value = '';
    }
};

// Busy indicator for partial postbacks.
clinic.beginRequest = function () {
    document.body.className = (document.body.className + ' busy').replace(/^\s+/, '');
};

clinic.endRequest = function (sender, args) {
    document.body.className = document.body.className.replace(/\s*busy/g, '');
    if (args.get_error()) {
        var message = document.getElementById('clientError');
        if (message) {
            message.innerHTML = 'The request failed. Reload the page and try again.';
            message.style.display = 'block';
        }
        args.set_errorHandled(true);
    }
    clinic.watchPatientBoxes();
};

Sys.Application.add_init(function () {
    var prm = Sys.WebForms.PageRequestManager.getInstance();
    prm.add_beginRequest(clinic.beginRequest);
    prm.add_endRequest(clinic.endRequest);
});

Sys.Application.add_load(clinic.watchPatientBoxes);

if (typeof (Sys) !== 'undefined') {
    Sys.Application.notifyScriptLoaded();
}
