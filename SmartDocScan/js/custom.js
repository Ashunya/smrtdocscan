$(document).ready(function() {
$(".list-unstyled li").click(function () {
    $(".list-unstyled li").removeClass("active");
    // $(".tab").addClass("active"); // instead of this do the below 
    $(this).addClass("active");   
});

/* Active menu */
$('#sidebarCollapse').on('click', function () {
$('#sidebar').toggleClass('active');
});

/* Sticky menu */
$(".sidebar-header").sticky({topSpacing: 0, zIndex:15});
$(".components").sticky({topSpacing: 74, zIndex:15});
$(".navbar").sticky({topSpacing: 0, zIndex:15});

/* Date Picker */
$("#datepicker").datepicker();
$('.fa-calendar').click(function() {
$("#datepicker").focus();
});

});





/* jSticky*/
!function(t){t.fn.sticky=function(s){function i(){return"number"==typeof o.zIndex?!0:!1}function e(){return 0<t(o.stopper).length||"number"==typeof o.stopper?!0:!1}var n={topSpacing:0,zIndex:"",stopper:".sticky-stopper",stickyClass:!1},o=t.extend({},n,s),r=i(),p=e();return this.each(function(){function s(){var s=u.scrollTop(),n=f,h=i.parent().width();if(l.width(h),p&&"string"==typeof f){var y=t(f).offset().top;n=y-e-c}if(s>d){if(o.stickyClass&&i.addClass(o.stickyClass),i.after(l).css({position:"fixed",top:c,width:h}),r&&i.css({zIndex:a}),p&&s>n){var v=n-s+c;i.css({top:v})}}else o.stickyClass&&i.removeClass(o.stickyClass),i.css({position:"static",top:null,left:null,width:"auto"}),l.remove()}var i=t(this),e=i.outerHeight(),n=i.outerWidth(),c=o.topSpacing,a=o.zIndex,d=i.offset().top-c,l=t("<div></div>").width(n).height(e).addClass("sticky-placeholder"),f=o.stopper,u=t(window);u.innerHeight()>e&&(u.bind("scroll",s),u.bind("load",s),u.bind("resize",s))})}}(jQuery);

/* dropbox */
!function(e){e.fn.niceSelect=function(t){function s(t){t.after(e("<div></div>").addClass("nice-select").addClass(t.attr("class")||"").addClass(t.attr("disabled")?"disabled":"").attr("tabindex",t.attr("disabled")?null:"0").html('<span class="current"></span><ul class="list"></ul>'));var s=t.next(),n=t.find("option"),i=t.find("option:selected");s.find(".current").html(i.data("display")||i.text()),n.each(function(t){var n=e(this),i=n.data("display");s.find("ul").append(e("<li></li>").attr("data-value",n.val()).attr("data-display",i||null).addClass("option"+(n.is(":selected")?" selected":"")+(n.is(":disabled")?" disabled":"")).html(n.text()))})}if("string"==typeof t)return"update"==t?this.each(function(){var t=e(this),n=e(this).next(".nice-select"),i=n.hasClass("open");n.length&&(n.remove(),s(t),i&&t.next().trigger("click"))}):"destroy"==t?(this.each(function(){var t=e(this),s=e(this).next(".nice-select");s.length&&(s.remove(),t.css("display",""))}),0==e(".nice-select").length&&e(document).off(".nice_select")):console.log('Method "'+t+'" does not exist.'),this;this.hide(),this.each(function(){var t=e(this);t.next().hasClass("nice-select")||s(t)}),e(document).off(".nice_select"),e(document).on("click.nice_select",".nice-select",function(t){var s=e(this);e(".nice-select").not(s).removeClass("open"),s.toggleClass("open"),s.hasClass("open")?(s.find(".option"),s.find(".focus").removeClass("focus"),s.find(".selected").addClass("focus")):s.focus()}),e(document).on("click.nice_select",function(t){0===e(t.target).closest(".nice-select").length&&e(".nice-select").removeClass("open").find(".option")}),e(document).on("click.nice_select",".nice-select .option:not(.disabled)",function(t){var s=e(this),n=s.closest(".nice-select");n.find(".selected").removeClass("selected"),s.addClass("selected");var i=s.data("display")||s.text();n.find(".current").text(i),n.prev("select").val(s.data("value")).trigger("change")}),e(document).on("keydown.nice_select",".nice-select",function(t){var s=e(this),n=e(s.find(".focus")||s.find(".list .option.selected"));if(32==t.keyCode||13==t.keyCode)return s.hasClass("open")?n.trigger("click"):s.trigger("click"),!1;if(40==t.keyCode){if(s.hasClass("open")){var i=n.nextAll(".option:not(.disabled)").first();i.length>0&&(s.find(".focus").removeClass("focus"),i.addClass("focus"))}else s.trigger("click");return!1}if(38==t.keyCode){if(s.hasClass("open")){var l=n.prevAll(".option:not(.disabled)").first();l.length>0&&(s.find(".focus").removeClass("focus"),l.addClass("focus"))}else s.trigger("click");return!1}if(27==t.keyCode)s.hasClass("open")&&s.trigger("click");else if(9==t.keyCode&&s.hasClass("open"))return!1});var n=document.createElement("a").style;return n.cssText="pointer-events:auto","auto"!==n.pointerEvents&&e("html").addClass("no-csspointerevents"),this}}(jQuery);

$(document).ready(function() {
	$('.wide:not(.ignore)').niceSelect();
});