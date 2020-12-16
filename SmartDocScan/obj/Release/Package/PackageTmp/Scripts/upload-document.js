
function onSuccess(data) {
    if (data.StatusCode == 300) {
        showToasterMsg(data.Message, 2);
    }
    else {
        showToasterMsg(data.Message, 1);
        setTimeout(
            function () {
                window.location.reload();
            }, 2000);
    }
}

function onFailure(data) {

}
$(function () {
    $('#formUploadDocument').submit(function (e) {
        var ext = $('input[name="doc_name"]').val().split('.').pop().toLowerCase();
        if (ext.length > 0) {
            if ($.inArray(ext, ['pdf','tiff']) == -1) {
                showToasterMsg('select file of type .pdf,.tiff', 2);
                return false;
            }
        }
        if ($(this).valid()) {

            var form = e.target;
            if (form.getAttribute("enctype") === "multipart/form-data") {
                if (form.dataset.ajax) {
                    e.preventDefault();
                    e.stopImmediatePropagation();

                    var xhr = new XMLHttpRequest();
                    xhr.open(form.method, form.action);
                    xhr.onreadystatechange = function () {
                        if (xhr.readyState == 4 && xhr.status == 200) {
                            if (form.dataset.ajaxUpdate) {
                                var updateTarget = document.querySelector(form.dataset.ajaxUpdate);
                                if (updateTarget) {
                                    updateTarget.innerHTML = xhr.responseText;
                                }
                            }
                            var res = $.parseJSON(xhr.responseText);
                            onSuccess(res);
                        }
                        else {
                            var res = $.parseJSON(xhr.responseText);
                            onFailure(res);
                        }
                    };
                    xhr.send(new FormData(form));
                }
            }
        }
    });
});


