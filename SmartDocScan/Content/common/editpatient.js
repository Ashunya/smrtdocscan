

$(document).ready(function () {
    
        $('#comp_id').val(getParameterByName('id'));
    });

        function onSuccess(data) {
        if (!data || data.Status === false || data.StatusCode == 300 || data.StatusCode == 400) {
            showToasterMsg(data && data.Message ? data.Message : 'Patient could not be saved.', 2);
            return;
        }
        showToasterMsg(data.Message, 1);

    setTimeout(
        function () {           
        window.location = '/admin/findpatient?id=' + getParameterByName('id');
    }, 2000);

}

        function onFailure(data) {
        showToasterMsg(data && data.Message ? data.Message : 'Patient could not be saved.', 2);
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
  
