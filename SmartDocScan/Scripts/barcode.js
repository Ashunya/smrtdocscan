$(document).ready(function () {
    $("#cat_id").change(function () {
        var cid = $('option:selected', this).val();
        var pid = getParameterByName('id');
        var url = '/admin/generatebarcode?pid=' + pid + '&cid=' + cid;
        $('#scanSepFrame').attr('src', url);
    });

    $("#scanSepPrint").click(function () {
        $('#scanSepFrame')[0].contentWindow.print();
    });
});