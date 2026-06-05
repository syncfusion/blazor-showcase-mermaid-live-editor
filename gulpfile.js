//ImportHere
var gulp = require("gulp");
const fs = require('fs');
const cleanCSS = require('gulp-clean-css');
const rename = require('gulp-rename');
const uglify = require('gulp-uglify');

// Minify JS files in wwwroot/js/
gulp.task('minify-js', function () {
    return gulp.src(
        ['Common/wwwroot/**/*.js', '!**/*.min.js'],
        { base: './' }
    )
    .pipe(uglify())
    .pipe(rename({ suffix: '.min' }))
    .pipe(gulp.dest('./'));
});

// CSS Minify Task
gulp.task('minify-css', function () {
    return gulp.src(
        ['Common/wwwroot/**/*.css', '!Common/wwwroot/**/*.min.css'],
        { base: './' }
    )
    .pipe(cleanCSS({ compatibility: 'ie8' }))
    .pipe(rename({ suffix: '.min' }))
    .pipe(gulp.dest('./'));
});

//replace placeholder with actual require statement
gulp.task('dynamic-import', async function(){
    var gulpfile = await fs.readFileSync('./gulpfile.js', 'utf-8');
    gulpfile = gulpfile.replace('//ImportHere', 'require("@syncfusion/ej2-staging");');
    await fs.writeFileSync('./gulpfile.js', gulpfile);
});

