/**
 *
 * You can write your JS code here, DO NOT touch the default style file
 * because it will make it harder for you to update.
 * 
 */

"use strict";

$(document).ready(function() {
    $(document).on('click', '.mobile-expand-btn', function() {
        var $btn = $(this);
        var $tr = $btn.closest('tr');
        
        $tr.toggleClass('expanded');
        
        var $icon = $btn.find('i');
        if ($tr.hasClass('expanded')) {
            $icon.attr('data-feather', 'chevron-up');
        } else {
            $icon.attr('data-feather', 'chevron-down');
        }
        
        if (typeof feather !== 'undefined') {
            feather.replace();
        }
    });
});
