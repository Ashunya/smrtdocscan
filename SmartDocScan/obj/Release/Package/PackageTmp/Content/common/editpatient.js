

$(document).ready(function () {
    
        $('#comp_id').val(getParameterByName('id'));
    });

        function onSuccess(data) {
        showToasterMsg(data.Message, 1);

    setTimeout(
        function () {           
        window.location = '/admin/findpatient?id=1';
    }, 2000);

}

        function onFailure(data) {
        showToasterMsg(data.Message, 2);
    }
        $(function () {
        $('#formPatient').submit(function (e) {
            if ($(this).valid()) {
                var form = e.target;
                if (form.dataset.ajax) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    var xhr = new XMLHttpRequest();
                    xhr.open(form.method, form.action);
                    xhr.onreadystatechange = function () {
                        if (xhr.readyState === 4 && xhr.status === 200) {
                            if (form.dataset.ajaxUpdate) {
                                var updateTarget = document.querySelector(form.dataset.ajaxUpdate);
                                if (updateTarget) {
                                    updateTarget.innerHTML = xhr.responseText;
                                }
                            }
                            var res = $.parseJSON(xhr.responseText);
                            onSuccess(res);
                        }
                    };
                    xhr.send(new FormData(form));
                }
            }
        });
    });
  