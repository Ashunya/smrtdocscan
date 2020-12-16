var dTable;
var dTable1;

var jsDataTable = '#tblBox';
var jsDataTable1 = '#tblPatient';

$(document).ready(function () {
    dTable = $(jsDataTable).DataTable({
        "sPaginationType": "full_numbers",
        "pageLength": 10,
        "bProcessing": true,
        "sAjaxSource": "/Admin/AddBoxBindGrid",
        oLanguage: { sProcessing: showLoader() },
        "fnDrawCallback": function (oSettings) {
            setTimeout(
                function () {
                    hideLoader();
                }, 1000);
        },
        "fnServerParams": function (aoData) {
            aoData.push({ "name": "companyId", "value": getParameterByName('id') });
        },
        aoColumns: [
            { "data": "box_ext_id", "sClass": "text-center" },
            { "data": "box_name", "sClass": "text-center" },
            { "data": "aisle", "sClass": "text-center" },
            { "data": "section", "sClass": "text-center" },
            { "data": "brow", "sClass": "text-center" },
            { "data": "bcolumn", "sClass": "text-center" },
        ]
    });

});

$(document).ready(function () {    $('#comp_id').val(getParameterByName('id'));
    $(".addBtnNewBox").click(function () {        $("#addNewBox").show();        $("#boxId26").hide();    });


    $(".CancelBtnNewBox").click(function () {        $("#addNewBox").hide();    });

    $('#tblBox tbody').on('click', 'tr', function () {
        $("#addNewBox").hide();


        $("#boxId26").show();

        var id = dTable.row(this).data().box_id;

        $('#spnBoxId').val(id);

        $('#tblPatient').DataTable().clear().destroy();

        dTable1 = $(jsDataTable1).DataTable({
            "sPaginationType": "full_numbers",
            "pageLength": 10,
            "bProcessing": true,
            "sAjaxSource": "/Admin/PatientBindGrid",
            oLanguage: { sProcessing: showLoader() },
            "fnDrawCallback": function (oSettings) {
                setTimeout(
                    function () {
                        hideLoader();
                    }, 1000);
            },
            "fnServerParams": function (aoData) {
                aoData.push({ "name": "companyId", "value": getParameterByName('id') });
                aoData.push({ "name": "boxId", "value": id });
            },
            aoColumns: [
                { "data": "patient_id", "sClass": "text-center" },
                { "data": "first_name", "sClass": "text-center" },
                { "data": "last_name", "sClass": "text-center" },
            ]
        });
    });

});




function onSuccess(data) {
    debugger;
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
    $('#formBox').submit(function (e) {
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


